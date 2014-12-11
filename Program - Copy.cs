using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using SunEdison.Core.Security;
using SunEdison.Framework.Security;
using SunEdison.Sam.Design;
using SunEdison.Sam.Energy;
using SunEdison.Sam.Framework;
using SunEdison.Sam.Planning;
using SunEdison.Sam.Projects;
using SunEdison.Sam.Core.Monitoring;
using System.Linq;


namespace RefDataToolkit
{
    internal delegate void ParseLineHandler(EnergyEstimateImportItem stringValues);

    public class EnergyEstimateImportItem
    {
        public string ProjectNumber { get; set; }
        public List<Degradation> Degradations { get; set; }
    }

    public class Degradation
    {
        public double Rate { get; set; }
        public int Year { get; set; }
    }

    public class Program
    {
        private static SunPrincipal _sunPrincipal;
        private static SEPrincipal _sePrincipal;

        private static string _insolationPath;
        private static double _lastDegradationFactor;

        public static void Main(string[] args)
        {

            //args = new string[1];
            //args[0] = "/i";

            // Initialize input arguments
            bool insolationOnly = false;

            foreach (var arg in args)
            {
                var splitArg = arg.Split('=');
                string value = "";

                if (splitArg != null && splitArg.Count() > 0)
                {
                    var command = splitArg[0].ToLower();
                    if (splitArg.Count() > 1)
                        value = splitArg[1].ToLower().Trim();

                    switch (command)
                    {
                        //interval
                        case "/i":
                            insolationOnly = true;
                            break;
                    }
                }
            }


            // create IPrincipal
            string directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string path = Assembly.GetExecutingAssembly().Location + ".config";
            RemotingConfiguration.Configure(path, true);
            _sunPrincipal = SunPrincipalFactory.CreateSystemSunPrincipal(); // For SAM API calls
            _sePrincipal = SEPrincipalFactory.CreateSystemSEPrincipal();    // For SMART API Calls


            // Get insolation path from Configuration
            //_insolationPath = System.Configuration.ConfigurationManager.AppSettings.Get("InsolationPath");
            _insolationPath = string.Format("{0}\\{1}\\", directory, "insolation");


            // JobStart Heading
            Console.WriteLine("--------------------------");
            Console.WriteLine("Reference Data Import Tool");
            Console.WriteLine("--------------------------");
            Console.WriteLine();
            Console.WriteLine("This tool is used to import Energy Estimates from SAM into SMART.");
            Console.WriteLine();


            // import reference data         
            ImportInsolation();

            if (!insolationOnly)
            {
                ImportEnergyEstimates();
            }

            Console.WriteLine();
            Console.WriteLine();

        }

        private static void ImportRefInsolation(DateTime cod, double[] monthly)
        {
            DateTime start = cod;
            DateTime end = start.AddYears(35).AddDays(-1);
            DateTime date = start;
        }

        private static void ImportEnergyEstimates()
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Import reference data into SMART...");

                ImportEnergyToSMART(ImportEnergy, 2);
            }
            finally
            {
                Console.WriteLine("DONE");
                Console.ReadLine();
            }
        }

        // The "import" method calls the handler delegate one time for each line in the file.
        // The file needs to be tab delimited and contain "columnCount" columns.
        // If the handler throws an exception, it will be handled gracefully and the error will be outputed to the Console.
        private static void ImportEnergyToSMART(ParseLineHandler handler, int columnCount)
        {
            List<EnergyEstimateImportItem> energyImportItems = GetRefDataFromFile();

            int lineNumber = 0;

            foreach (var energyImportItem in energyImportItems)
            {
                try
                {
                    lineNumber++;
                    Console.WriteLine(string.Format("Importing project #{0}: {1}", lineNumber, energyImportItem.ProjectNumber));
                    handler(energyImportItem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error on project " + energyImportItem.ProjectNumber);
                    Console.WriteLine(ex.Message);
                }
            }
        }

        // Column 1 = Project #
        // Column 2 = Decay rate1|year (rate in decimal ie 0.005 not 0.5%, year in integer applying to rate1. If no year, defaul to lifespan of the plant, 35 years)
        // Column 3 = Decay rate2|year (rate in decimal ie 0.005 not 0.5%, year in integer applying to rate2. This is optional)
        // The assumption is that the project has a Commercial Operation date; that is the date the projection will begin
        private static void ImportEnergy(EnergyEstimateImportItem energyImportItem)
        {
            SunPrincipal.ToCallContext(_sunPrincipal);

            string projectNumber = energyImportItem.ProjectNumber;

            Project project = ServiceManager.Projects.GetProjectForProjectNumber(projectNumber);
            List<PVSystem> pvSystemList = ServiceManager.Design.GetPVSystemListForProjectID(project.ProjectID);
            ProjectPlan projectPlan = ServiceManager.Planning.GetProjectPlan(EntityType.Project, project.ProjectID);
            DateTime cod = projectPlan.GetMilestone(MilestoneType.CommercialOperation).ActualEndDate.Value;

            double[] adjMonthly = new double[12];

            // Get 12 months projections from SAM
            EnergyEstimateHistory energyHistory = ServiceManager.Energy.GetEnergyEstimateHistoryForProjectID(project.ProjectID);

            if (energyHistory == null)
            {
                Console.Write("      (No Energy Estimates in SAM)");
                Console.WriteLine();
            }
            else
            {
                energyHistory.EnergyEstimateMonthList.Sort((a, b) => a.Month.CompareTo(b.Month));

                int month = 0;
                foreach (var estimateMonth in energyHistory.EnergyEstimateMonthList)
                {
                    double? refEnergy = estimateMonth.EnergyAfterLosses;
                    //double? refInsolation = estimateMonth.GlobalIncident;
                    double? refInsolation = estimateMonth.GlobalHorizon;

                    adjMonthly[month] = Convert.ToDouble(refEnergy / pvSystemList.Count);
                    month++;
                }

                SunPrincipal.ClearCallContext();

                DateTime startDate = cod;
                DateTime lastDate = cod;
                bool deleteSeries = true;
                double degradeRateStart = 1.00;

                foreach (var degradation in energyImportItem.Degradations)
                {
                    double degradeRate = degradation.Rate;
                    int degradeYear = degradation.Year;

                    if (degradeYear <= 0)
                    {
                        degradeYear = 35;   //default to 35 years if there is only one rate available
                    }

                    // Load estimates
                    foreach (PVSystem pvSystem in pvSystemList)
                    {
                        EnergyEstimateNew(pvSystem.PVSystemID, startDate, adjMonthly, degradeRate, degradeYear, degradeRateStart, 109, deleteSeries, useFOD9Table: true); //Load to channel 109 in FO_D9 Table
                        EnergyEstimateNew(pvSystem.PVSystemID, startDate, adjMonthly, degradeRate, degradeYear, degradeRateStart, 109, deleteSeries, useFOD9Table: false); //Load to channel 109 in SB_D5 table
                    }

                    // start date for the next degradation period
                    startDate = startDate.AddYears(degradeYear).AddDays(1);
                    deleteSeries = false;
                    degradeRateStart = _lastDegradationFactor;

                }
            }

        }


        private static double GetLeapYearFactor(DateTime date, double febTotal, double feb28)
        {
            if (((double)date.Year / 4) == (int)((double)date.Year / 4) && date.Month == 2)
                return febTotal / (febTotal + feb28);
            return 1;
        }

        
        private static void EnergyEstimateNew(int pvSystemID,
                                                DateTime startDate,
                                                double[] monthly,
                                                double annualDegradeRate,
                                                int degradeYear,
                                                double degradeRateStart,
                                                int channelID,
                                                bool deleteSeries,
                                                bool useFOD9Table = false)
        {

            //cng 7-30-14. the Reference Data is migrated from SB_D5 table to FO_D9 tabe to support higher precision.

            SqlConnection cnn = null;

            try
            {
                SEPrincipal.ToCallContext(_sePrincipal);
                Source source = ServiceManager.Monitoring.GetSource(SourceType.PVSystem, pvSystemID, true);
                Series series = ServiceManager.Monitoring.GetPrimarySeries(source.SourceID, channelID, true);
                SEPrincipal.ClearCallContext();
                
                SunPrincipal.ToCallContext(_sunPrincipal);

                double total = 0;
                for (int i = 0; i < 12; i++)
                    total += monthly[i];
                PVSystem pvSystem = ServiceManager.Design.GetPVSystem(pvSystemID);
                pvSystem.WarranteedYear1Output = total;
                ServiceManager.Design.UpdatePVSystem(pvSystem);

                double[] energyPerDay;
                EnergyEstimater estimater = new EnergyEstimater();
                estimater.Calculate(monthly, out energyPerDay);

                DateTime start = startDate;
                DateTime end = start.AddYears(degradeYear).AddDays(-1);
                DateTime date = start;
                int dayOfYear = date.DayOfYear - 1;
                if (((double)date.Year / 4) == (int)((double)date.Year / 4) && date.Month > 2)
                    dayOfYear--;
                double factor = degradeRateStart;

                SunPrincipal.ClearCallContext();

                cnn = new SqlConnection(@"Data Source=10.8.65.3\prod_sf;Initial Catalog=SOIL_PROD;User ID=SAM_USER;password=#se2007");
                cnn.Open();

                StringBuilder builder = new StringBuilder();
                StringWriter writer = new StringWriter(builder);
                string sql = "";

                if (deleteSeries)
                {
                    if (useFOD9Table)
                        sql = "EXEC DAT.SP_FO_D9_DELETE_ALL " + series.SeriesID;
                    else
                        sql = "EXEC DAT.SP_SB_D5_DELETE_ALL " + series.SeriesID;

                    writer.WriteLine(sql);
                }

                while (date <= end)
                {
                    double leapYearFactor = Program.GetLeapYearFactor(date, monthly[1], energyPerDay[31 + 28 - 1]);
                    double refEnergy = energyPerDay[dayOfYear] * factor * leapYearFactor;

                    if (useFOD9Table)
                        sql = string.Format("EXEC DAT.SP_FO_D9_ADD {0}, {1}, 0, 0, {2}", series.SeriesID, +date.Subtract(new DateTime(2000, 1, 1)).TotalDays, refEnergy);
                    else
                        sql = string.Format("EXEC DAT.SP_SB_D5_ADD {0}, {1}, 0, 0, {2}", series.SeriesID, +date.Subtract(new DateTime(2000, 1, 1)).TotalDays, refEnergy);

                    writer.WriteLine(sql);

                    //Console.WriteLine(refEnergy);

                    date = date.AddDays(1);
                    if (!(date.Month == 2 && date.Day == 29))
                    {
                        dayOfYear++;
                    }
                    if (date.Month == 1 && date.Day == 1)
                        dayOfYear = 0;
                    if (date.Month == start.Month && date.Day == start.Day)
                        factor *= (1 - annualDegradeRate);
                }

                writer.Close();

                sql = builder.ToString();

                SqlCommand cmd = new SqlCommand(sql, cnn);
                cmd.CommandTimeout = 1000000;
                cmd.ExecuteNonQuery();

                //Save last data point of the degradation period
                _lastDegradationFactor = factor;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (cnn != null)
                    cnn.Close();

            }
        }

        private static void ImportInsolation()
        {
            SunPrincipal sunPrincipal = SunPrincipalFactory.CreateSystemSunPrincipal();
            SunPrincipal.ToCallContext(sunPrincipal);

            List<EnergyEstimateImportItem> energyImportItems = GetRefDataFromFile();

            try
            {
                string path = _insolationPath;

                if (!System.IO.Directory.Exists(path))
                    System.IO.Directory.CreateDirectory(path);

                DeleteAllFiles(path);

                Console.Write("Export insolation to files...");
                


                foreach (var energyImportItem in energyImportItems)
                {
                    Project project = ServiceManager.Projects.GetProjectForProjectNumber(energyImportItem.ProjectNumber);
                    EnergyEstimateHistory history = ServiceManager.Energy.GetEnergyEstimateHistoryForProjectID(project.ProjectID);

                    if (history != null)
                    {

                        history.EnergyEstimateMonthList.Sort((a, b) => a.Month.CompareTo(b.Month));

                        string projectName = project.ProjectName;
                        string projectNo = project.ProjectNumber;

                        projectName = projectName.Replace(",", "-");
                        projectName = projectName.Replace("/", " ");
                        projectName = projectName.Replace("\\", " ");

                        // Check if there are reference insolation (GlobalInclined insolation) in SAM Energy Estimates
                        if (history.EnergyEstimateMonthList != null && history.EnergyEstimateMonthList[0].GlobalIncident != null)
                        {
                            // create csv file
                            string filename = string.Format("{0}", projectNo);
                            string file = string.Format("{0}{1}.csv", path, filename);

                            StreamWriter sw = new StreamWriter(file);

                            foreach (var estimateMonth in history.EnergyEstimateMonthList)
                            {
                                int month = estimateMonth.Month;
                                sw.WriteLine("{0},{1}", estimateMonth.Month, estimateMonth.GlobalIncident);
                                //sw.WriteLine("{0},{1}", estimateMonth.Month, estimateMonth.GlobalHorizon);

                                sw.Flush();
                            }
                        }

                    }
                    else
                    {
                        Console.WriteLine(string.Format("{0}: No estimate", energyImportItem.ProjectNumber));
                    }
                }

                Console.Write("Done");

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                SunPrincipal.ClearCallContext();
            }

        }

        private static void DeleteAllFiles(string path)
        {

            System.IO.DirectoryInfo downloadedMessageInfo = new DirectoryInfo(path);

            foreach (FileInfo file in downloadedMessageInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in downloadedMessageInfo.GetDirectories())
            {
                dir.Delete(true);
            }

        }

        private static List<EnergyEstimateImportItem> GetRefDataFromFile()
        {
            string importFile;
            string directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            importFile = Path.Combine(directory, "energy.txt");

            StreamReader reader = new StreamReader(importFile);
            int lineNumber = 0;

            List<EnergyEstimateImportItem> importItems = new List<EnergyEstimateImportItem>();

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                lineNumber++;
                try
                {
                    string[] stringValues = line.Split("\t".ToCharArray());
                    if (stringValues.Length >= 2)
                    {
                        string projectNumber = stringValues[0];
                        List<Degradation> degradations = new List<Degradation>();

                        for (var i = 1; i < stringValues.Length; i++)
                        {
                            var rate = stringValues[i];
                            double degradationRate;
                            int degradationYear = -1;

                            string[] rateValuePairs = rate.Split('|');

                            degradationRate = Convert.ToDouble(rateValuePairs[0]);
                            if (rateValuePairs.Length == 2)
                            {
                                degradationYear = Convert.ToInt32(rateValuePairs[1]);
                            }

                            Degradation degradation = new Degradation();
                            degradation.Rate = degradationRate;
                            degradation.Year = degradationYear;
                            degradations.Add(degradation);
                        }

                        EnergyEstimateImportItem importItem = new EnergyEstimateImportItem();
                        importItem.ProjectNumber = projectNumber;
                        importItem.Degradations = degradations;
                        importItems.Add(importItem);

                    }
                    else
                        throw new Exception("Incorrect # of values");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error on line #" + lineNumber);
                    Console.WriteLine(ex.Message);
                }
            }

            return importItems;
        }

        //private static void ImportRefData()
        //{
        //    List<EnergyEstimateImportItem> energyImportItems = GetRefDataFromFile();

        //    int lineNumber = 0;

        //    foreach (var energyImportItem in energyImportItems)
        //    {
        //        try
        //        {
        //            lineNumber++;
        //            Console.WriteLine(string.Format("Parsing line #{0}: {1}", lineNumber, energyImportItem.ProjectNumber));
        //            ImportData(energyImportItem);
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("Error on line #" + lineNumber);
        //            Console.WriteLine(ex.Message);
        //        }
        //    }
        //}


        //private static void ImportData(EnergyEstimateImportItem energyImportItem)
        //{
        //    SunPrincipal.ToCallContext(_sunPrincipal);

        //    string projectNumber = energyImportItem.ProjectNumber;

        //    Project project = ServiceManager.Projects.GetProjectForProjectNumber(projectNumber);
        //    List<PVSystem> pvSystemList = ServiceManager.Design.GetPVSystemListForProjectID(project.ProjectID);
        //    ProjectPlan projectPlan = ServiceManager.Planning.GetProjectPlan(EntityType.Project, project.ProjectID);
        //    DateTime cod = projectPlan.GetMilestone(MilestoneType.CommercialOperation).ActualEndDate.Value;            

        //    double decay = energyImportItem.DegradationRate;
        //    double[] monthlyRefEnergy = new double[12];
        //    double[] monthlyRefInsolation = new double[12];

        //    // Get 12 months projections
        //    EnergyEstimateHistory energyHistory = ServiceManager.Energy.GetEnergyEstimateHistoryForProjectID(project.ProjectID);

        //    if (energyHistory != null)
        //    {
        //        energyHistory.EnergyEstimateMonthList.Sort((a, b) => a.Month.CompareTo(b.Month));

        //        int month = 0;
        //        foreach (var estimateMonth in energyHistory.EnergyEstimateMonthList)
        //        {
        //            double? refEnergy = estimateMonth.EnergyAfterLosses;
        //            double? refInsolation = estimateMonth.GlobalIncident;
        //            monthlyRefEnergy[month] = Convert.ToDouble(refEnergy / pvSystemList.Count);
        //            monthlyRefInsolation[month] = Convert.ToDouble(refInsolation);
        //            month++;
        //        }

        //        SunPrincipal.ClearCallContext();

        //        // Load energy estimates
        //        foreach (PVSystem pvSystem in pvSystemList)
        //        {
        //            ImportRefEnergyEstimate(pvSystem.PVSystemID, cod, monthlyRefEnergy, decay, 109); //Load to channel 109
        //        }

        //    }

        //}


        //private static void ImportRefEnergyEstimate(int pvSystemID, DateTime cod, double[] monthly, double annualDegradeRate, double lastDegradeFactor, int channelID)
        //{

        //    SEPrincipal.ToCallContext(_sePrincipal);
        //    Source source = ServiceManager.Monitoring.GetSource(SourceType.PVSystem, pvSystemID, true);
        //    Series series = ServiceManager.Monitoring.GetPrimarySeries(source.SourceID, channelID, true);
        //    SEPrincipal.ClearCallContext();

        //    SunPrincipal.ToCallContext(_sunPrincipal);

        //    double total = 0;
        //    for (int i = 0; i < 12; i++)
        //        total += monthly[i];
        //    PVSystem pvSystem = ServiceManager.Design.GetPVSystem(pvSystemID);
        //    pvSystem.WarranteedYear1Output = total;
        //    ServiceManager.Design.UpdatePVSystem(pvSystem);

        //    double[] energyPerDay;
        //    EnergyEstimater estimater = new EnergyEstimater();
        //    estimater.Calculate(monthly, out energyPerDay);

        //    DateTime start = cod;
        //    DateTime end = start.AddYears(35).AddDays(-1);
        //    DateTime date = start;
        //    int dayOfYear = date.DayOfYear - 1;
        //    if (((double)date.Year / 4) == (int)((double)date.Year / 4) && date.Month > 2)
        //        dayOfYear--;
        //    double factor = lastDegradeFactor;

        //    SunPrincipal.ClearCallContext();

        //    SqlConnection cn = new SqlConnection(@"Data Source=10.8.65.3\prod_sf;Initial Catalog=SOIL_PROD;User ID=SAM_USER;password=#se2007");
        //    cn.Open();
        //    try
        //    {
        //        StringBuilder builder = new StringBuilder();
        //        StringWriter writer = new StringWriter(builder);
        //        string sql = "EXEC DAT.SP_SB_D5_DELETE_ALL " + series.SeriesID;
        //        writer.WriteLine(sql);
        //        while (date <= end)
        //        {
        //            double leapYearFactor = Program.GetLeapYearFactor(date, monthly[1], energyPerDay[31 + 28 - 1]);
        //            sql = string.Format("EXEC DAT.SP_SB_D5_ADD {0}, {1}, 0, 0, {2}", series.SeriesID, +date.Subtract(new DateTime(2000, 1, 1)).TotalDays, energyPerDay[dayOfYear] * factor * leapYearFactor);
        //            writer.WriteLine(sql);

        //            date = date.AddDays(1);
        //            if (!(date.Month == 2 && date.Day == 29))
        //            {
        //                dayOfYear++;
        //            }
        //            if (date.Month == 1 && date.Day == 1)
        //                dayOfYear = 0;
        //            if (date.Month == start.Month && date.Day == start.Day)
        //                factor *= (1 - annualDegradeRate);
        //        }

        //        writer.Close();
        //        SqlCommand cmd = new SqlCommand(builder.ToString(), cn);
        //        cmd.CommandTimeout = 600000;
        //        cmd.ExecuteNonQuery();

        //        // Save last degradation data
        //        _lastDegradationDate = date;
        //        _lastDegradationFactor = factor;            

        //    }
        //    finally
        //    {
        //        cn.Close();
        //    }
        //}

    }

}