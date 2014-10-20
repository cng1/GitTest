using SunEdison.Core.Remoting;
using SunEdison.Sam.Common;
using SunEdison.Sam.Design;
using SunEdison.Sam.Documents;
using SunEdison.Sam.Energy;
using SunEdison.Sam.Epc;
using SunEdison.Sam.FinancialPlanning;
using SunEdison.Sam.Framework;
using SunEdison.Sam.Gui;
using SunEdison.Sam.HumanResources;
using SunEdison.Sam.Inventory;
using SunEdison.Sam.MetaData;
using SunEdison.Sam.Mrp;
using SunEdison.Sam.Planning;
using SunEdison.Sam.Pricing;
using SunEdison.Sam.ProjectFinance;
using SunEdison.Sam.Report;
using SunEdison.Sam.Timberline;
using SunEdison.Sam.Workflow;
using SunEdison.Sam.Core.Logical;
using SunEdison.Sam.Core.Monitoring;
using SunEdison.Sam.Core.DataMining;
using SunEdison.Sam.Financial;
using SunEdison.Sam.Rec;
using SunEdison.Sam.Sales;
using SunEdison.Sam.Security;
using SunEdison.Sam.Projects;
using SunEdison.Sam.Core.Service;

namespace RefDataToolkit
{
	public class ServiceManager
	{
		private static readonly ICommonService common = RemotingFactory<ICommonService>.Create();
		private static readonly IDesignService design = RemotingFactory<IDesignService>.Create();
		private static readonly IDataMiningService dataMining = RemotingFactory<IDataMiningService>.Create();
		private static readonly IDocumentsService documents = RemotingFactory<IDocumentsService>.Create();
		private static readonly IEnergyService energy = RemotingFactory<IEnergyService>.Create();
		private static readonly IEpcService epc = RemotingFactory<IEpcService>.Create();
		private static readonly IFinancialService financial = RemotingFactory<IFinancialService>.Create();
		private static readonly IFinancialPlanningService financialPlanning = RemotingFactory<IFinancialPlanningService>.Create();
		private static readonly IFrameworkService framework = RemotingFactory<IFrameworkService>.Create();
		private static readonly IGuiService gui = RemotingFactory<IGuiService>.Create();
		private static readonly IHumanResourcesService humanResources = RemotingFactory<IHumanResourcesService>.Create();
		private static readonly IInventoryService inventory = RemotingFactory<IInventoryService>.Create();
		private static readonly ILogicalService soilLogical = RemotingFactory<ILogicalService>.Create();
		private static readonly IMetaDataService metaData = RemotingFactory<IMetaDataService>.Create();
		private static readonly IMonitoringService monitoring = RemotingFactory<IMonitoringService>.Create();
		private static readonly IMrpService mrp = RemotingFactory<IMrpService>.Create();
		private static readonly IPlanningService planning = RemotingFactory<IPlanningService>.Create();
		private static readonly IPricingService pricing = RemotingFactory<IPricingService>.Create();
		private static readonly IProjectsService projects = RemotingFactory<IProjectsService>.Create();
		private static readonly IProjectFinanceService projectFinance = RemotingFactory<IProjectFinanceService>.Create();
		private static readonly IRecService rec = RemotingFactory<IRecService>.Create();
		private static readonly IReportService report = RemotingFactory<IReportService>.Create();
		private static readonly ISalesService sales = RemotingFactory<ISalesService>.Create();
		private static readonly ISecurityService security = RemotingFactory<ISecurityService>.Create();
		private static readonly IServiceService ticketing = RemotingFactory<IServiceService>.Create();
		private static readonly ITimberlineService timberline = RemotingFactory<ITimberlineService>.Create();
		private static readonly IWorkflowService workflow = RemotingFactory<IWorkflowService>.Create();

		public static ICommonService Common { get { return common; } }
		public static IDesignService Design { get { return design; } }
		public static IDataMiningService DataMining { get { return dataMining; } }
		public static IDocumentsService Documents { get { return documents; } }
		public static IEnergyService Energy { get { return energy; } }
		public static IEpcService Epc { get { return epc; } }
		public static IFinancialService Financial { get { return financial; } }
		public static IFinancialPlanningService FinancialPlanning { get { return financialPlanning; } }
		public static IFrameworkService Framework { get { return framework; } }
		public static IGuiService Gui { get { return gui; } }
		public static IHumanResourcesService HumanResources { get { return humanResources; } }
		public static IInventoryService Inventory { get { return inventory; } }
		public static ILogicalService SoilLogical { get { return soilLogical; } }
		public static IMetaDataService MetaData { get { return metaData; } }
		public static IMonitoringService Monitoring { get { return monitoring; } }
		public static IMrpService Mrp { get { return mrp; } }
		public static IPlanningService Planning { get { return planning; } }
		public static IPricingService Pricing { get { return pricing; } }
		public static IProjectsService Projects { get { return projects; } }
		public static IProjectFinanceService ProjectFinance { get { return projectFinance; } }
		public static IRecService Rec { get { return rec; } }
		public static IReportService Report { get { return report; } }
		public static ISalesService Sales { get { return sales; } }
		public static ISecurityService Security { get { return security; } }
		public static IServiceService Ticketing { get { return ticketing; } }
		public static ITimberlineService Timberline { get { return timberline; } }
		public static IWorkflowService Workflow { get { return workflow; } }
	}
}
