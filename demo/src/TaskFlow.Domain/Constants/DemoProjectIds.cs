namespace TaskFlow.Domain.Constants;

/// <summary>
/// Well-known project IDs used for demo data and upcasting demonstrations
/// These IDs are stable across demo data regeneration
/// </summary>
public static class DemoProjectIds
{
    /// <summary>
    /// Projects using LEGACY Project.Completed event (will be upcasted)
    /// </summary>
    public static class Legacy
    {
        /// <summary>
        /// "Customer Portal Redesign" - Upcasts to ProjectCompletedSuccessfully
        /// </summary>
        public const string CustomerPortalRedesign = "10000000-0000-0000-0000-000000000001";

        /// <summary>
        /// "Marketing Automation Tool" - Upcasts to ProjectCancelled
        /// </summary>
        public const string MarketingAutomationTool = "10000000-0000-0000-0000-000000000002";

        /// <summary>
        /// "Social Media Integration" - Upcasts to ProjectFailed
        /// </summary>
        public const string SocialMediaIntegration = "10000000-0000-0000-0000-000000000003";

        /// <summary>
        /// "Reporting Engine" - Upcasts to ProjectDelivered
        /// </summary>
        public const string ReportingEngine = "10000000-0000-0000-0000-000000000004";

        /// <summary>
        /// "OAuth Provider Service" - Upcasts to ProjectSuspended
        /// </summary>
        public const string OAuthProviderService = "10000000-0000-0000-0000-000000000005";

        /// <summary>
        /// All legacy project IDs for filtering
        /// </summary>
        public static readonly string[] All = new[]
        {
            CustomerPortalRedesign,
            MarketingAutomationTool,
            SocialMediaIntegration,
            ReportingEngine,
            OAuthProviderService
        };
    }

    /// <summary>
    /// Projects using NEW specific outcome events
    /// </summary>
    public static class NewEvents
    {
        /// <summary>
        /// "Mobile Banking App" - Project.CompletedSuccessfully
        /// </summary>
        public const string MobileBankingApp = "20000000-0000-0000-0000-000000000001";

        /// <summary>
        /// "Data Analytics Platform" - Project.Cancelled
        /// </summary>
        public const string DataAnalyticsPlatform = "20000000-0000-0000-0000-000000000002";

        /// <summary>
        /// "Microservices Migration" - Project.Failed
        /// </summary>
        public const string MicroservicesMigration = "20000000-0000-0000-0000-000000000003";

        /// <summary>
        /// "GraphQL API Development" - Project.Delivered
        /// </summary>
        public const string GraphQLApiDevelopment = "20000000-0000-0000-0000-000000000004";

        /// <summary>
        /// "A/B Testing Framework" - Project.Suspended
        /// </summary>
        public const string ABTestingFramework = "20000000-0000-0000-0000-000000000005";

        /// <summary>
        /// All new event project IDs for filtering
        /// </summary>
        public static readonly string[] All = new[]
        {
            MobileBankingApp,
            DataAnalyticsPlatform,
            MicroservicesMigration,
            GraphQLApiDevelopment,
            ABTestingFramework
        };
    }

    /// <summary>
    /// All upcasting demo project IDs (both legacy and new)
    /// </summary>
    public static readonly string[] AllUpcastingDemoProjects = Legacy.All.Concat(NewEvents.All).ToArray();

    /// <summary>
    /// Projects demonstrating EventVersion / Schema Versioning
    /// These projects show how the same event name can have different schema versions
    /// </summary>
    public static class SchemaVersioning
    {
        /// <summary>
        /// "Enterprise CRM System" - Uses V1 MemberJoinedProject events (legacy, no permissions)
        /// Created 400+ days ago when permissions weren't tracked
        /// </summary>
        public const string EnterpriseCrmSystem = "30000000-0000-0000-0000-000000000001";

        /// <summary>
        /// "Cloud Security Platform" - Uses V2 MemberJoinedProject events (with permissions)
        /// Created recently with the new permission-aware member system
        /// </summary>
        public const string CloudSecurityPlatform = "30000000-0000-0000-0000-000000000002";

        /// <summary>
        /// "DevOps Pipeline Modernization" - Mixed: started with V1, later members added with V2
        /// Shows real-world migration scenario where old and new events coexist
        /// </summary>
        public const string DevOpsPipelineModernization = "30000000-0000-0000-0000-000000000003";

        /// <summary>
        /// All schema versioning demo project IDs for filtering
        /// </summary>
        public static readonly string[] All = new[]
        {
            EnterpriseCrmSystem,
            CloudSecurityPlatform,
            DevOpsPipelineModernization
        };
    }

    /// <summary>
    /// All demo project IDs across all demo categories
    /// </summary>
    public static readonly string[] AllDemoProjects = AllUpcastingDemoProjects
        .Concat(SchemaVersioning.All)
        .ToArray();
}
