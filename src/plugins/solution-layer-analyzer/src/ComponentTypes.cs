namespace Ddk.SolutionLayerAnalyzer;

/// <summary>
/// Constants for Dataverse component types.
/// </summary>
public static class ComponentTypes
{
    /// <summary>
    /// Entity (table) component type.
    /// </summary>
    public const string Entity = "Entity";

    /// <summary>
    /// Attribute (column) component type.
    /// </summary>
    public const string Attribute = "Attribute";

    /// <summary>
    /// Form component type.
    /// </summary>
    public const string Form = "SystemForm";

    /// <summary>
    /// View component type.
    /// </summary>
    public const string View = "SavedQuery";

    /// <summary>
    /// Chart component type.
    /// </summary>
    public const string Chart = "SavedQueryVisualization";

    /// <summary>
    /// Dashboard component type.
    /// </summary>
    public const string Dashboard = "SystemForm";

    /// <summary>
    /// Ribbon component type.
    /// </summary>
    public const string Ribbon = "RibbonCustomization";

    /// <summary>
    /// Web Resource component type.
    /// </summary>
    public const string WebResource = "WebResource";

    /// <summary>
    /// Plugin Step component type.
    /// </summary>
    public const string PluginStep = "SDKMessageProcessingStep";

    /// <summary>
    /// Business Process Flow component type.
    /// </summary>
    public const string BusinessProcessFlow = "Workflow";

    /// <summary>
    /// Model-Driven App component type.
    /// </summary>
    public const string ModelDrivenApp = "AppModule";

    /// <summary>
    /// Sitemap component type.
    /// </summary>
    public const string SiteMap = "SiteMap";

    /// <summary>
    /// Option Set component type.
    /// </summary>
    public const string OptionSet = "OptionSet";

    /// <summary>
    /// All supported component types for analysis.
    /// </summary>
    public static readonly string[] SupportedTypes = new[]
    {
        Entity,
        Attribute,
        Form,
        View,
        Chart,
        Dashboard,
        Ribbon,
        WebResource,
        PluginStep,
        BusinessProcessFlow,
        ModelDrivenApp,
        SiteMap,
        OptionSet
    };
}

/// <summary>
/// Component type codes used by Dataverse.
/// Reference: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/solutioncomponent#componenttype-choices
/// </summary>
public static class ComponentTypeCodes
{
    /// <summary>
    /// Entity (1).
    /// </summary>
    public const int Entity = 1;

    /// <summary>
    /// Attribute (2).
    /// </summary>
    public const int Attribute = 2;

    /// <summary>
    /// Relationship (3).
    /// </summary>
    public const int Relationship = 3;

    /// <summary>
    /// Attribute Picklist Value (4).
    /// </summary>
    public const int AttributePicklistValue = 4;

    /// <summary>
    /// Attribute Lookup Value (5).
    /// </summary>
    public const int AttributeLookupValue = 5;

    /// <summary>
    /// View Attribute (6).
    /// </summary>
    public const int ViewAttribute = 6;

    /// <summary>
    /// Localized Label (7).
    /// </summary>
    public const int LocalizedLabel = 7;

    /// <summary>
    /// Relationship Extra Condition (8).
    /// </summary>
    public const int RelationshipExtraCondition = 8;

    /// <summary>
    /// Option Set (9).
    /// </summary>
    public const int OptionSet = 9;

    /// <summary>
    /// Entity Relationship (10).
    /// </summary>
    public const int EntityRelationship = 10;

    /// <summary>
    /// Entity Relationship Role (11).
    /// </summary>
    public const int EntityRelationshipRole = 11;

    /// <summary>
    /// Entity Relationship Relationships (12).
    /// </summary>
    public const int EntityRelationshipRelationships = 12;

    /// <summary>
    /// Managed Property (13).
    /// </summary>
    public const int ManagedProperty = 13;

    /// <summary>
    /// Entity Key (14).
    /// </summary>
    public const int EntityKey = 14;

    /// <summary>
    /// Role (20).
    /// </summary>
    public const int Role = 20;

    /// <summary>
    /// Role Privilege (21).
    /// </summary>
    public const int RolePrivilege = 21;

    /// <summary>
    /// Display String (22).
    /// </summary>
    public const int DisplayString = 22;

    /// <summary>
    /// Display String Map (23).
    /// </summary>
    public const int DisplayStringMap = 23;

    /// <summary>
    /// Form (24).
    /// </summary>
    public const int Form = 24;

    /// <summary>
    /// Organization (25).
    /// </summary>
    public const int Organization = 25;

    /// <summary>
    /// Saved Query (26).
    /// </summary>
    public const int SavedQuery = 26;

    /// <summary>
    /// Workflow (29).
    /// </summary>
    public const int Workflow = 29;

    /// <summary>
    /// Report (31).
    /// </summary>
    public const int Report = 31;

    /// <summary>
    /// Report Entity (32).
    /// </summary>
    public const int ReportEntity = 32;

    /// <summary>
    /// Report Category (33).
    /// </summary>
    public const int ReportCategory = 33;

    /// <summary>
    /// Report Visibility (34).
    /// </summary>
    public const int ReportVisibility = 34;

    /// <summary>
    /// Attachment (35).
    /// </summary>
    public const int Attachment = 35;

    /// <summary>
    /// Email Template (36).
    /// </summary>
    public const int EmailTemplate = 36;

    /// <summary>
    /// Contract Template (37).
    /// </summary>
    public const int ContractTemplate = 37;

    /// <summary>
    /// KB Article Template (38).
    /// </summary>
    public const int KBArticleTemplate = 38;

    /// <summary>
    /// Mail Merge Template (39).
    /// </summary>
    public const int MailMergeTemplate = 39;

    /// <summary>
    /// Duplicate Rule (44).
    /// </summary>
    public const int DuplicateRule = 44;

    /// <summary>
    /// Duplicate Rule Condition (45).
    /// </summary>
    public const int DuplicateRuleCondition = 45;

    /// <summary>
    /// Entity Map (46).
    /// </summary>
    public const int EntityMap = 46;

    /// <summary>
    /// Attribute Map (47).
    /// </summary>
    public const int AttributeMap = 47;

    /// <summary>
    /// Ribbon Command (48).
    /// </summary>
    public const int RibbonCommand = 48;

    /// <summary>
    /// Ribbon Context Group (49).
    /// </summary>
    public const int RibbonContextGroup = 49;

    /// <summary>
    /// Ribbon Customization (50).
    /// </summary>
    public const int RibbonCustomization = 50;

    /// <summary>
    /// Ribbon Rule (52).
    /// </summary>
    public const int RibbonRule = 52;

    /// <summary>
    /// Ribbon Tab To Command Map (53).
    /// </summary>
    public const int RibbonTabToCommandMap = 53;

    /// <summary>
    /// Ribbon Diff (55).
    /// </summary>
    public const int RibbonDiff = 55;

    /// <summary>
    /// Saved Query Visualization (59).
    /// </summary>
    public const int SavedQueryVisualization = 59;

    /// <summary>
    /// System Form (60).
    /// </summary>
    public const int SystemForm = 60;

    /// <summary>
    /// Web Resource (61).
    /// </summary>
    public const int WebResource = 61;

    /// <summary>
    /// Site Map (62).
    /// </summary>
    public const int SiteMap = 62;

    /// <summary>
    /// Connection Role (63).
    /// </summary>
    public const int ConnectionRole = 63;

    /// <summary>
    /// Complex Control (64).
    /// </summary>
    public const int ComplexControl = 64;

    /// <summary>
    /// Hierarchy Rule (65).
    /// </summary>
    public const int HierarchyRule = 65;

    /// <summary>
    /// Custom Control (66).
    /// </summary>
    public const int CustomControl = 66;

    /// <summary>
    /// Custom Control Default Config (68).
    /// </summary>
    public const int CustomControlDefaultConfig = 68;

    /// <summary>
    /// Data Source Mapping (69).
    /// </summary>
    public const int DataSourceMapping = 69;

    /// <summary>
    /// SDK Message Processing Step (92).
    /// </summary>
    public const int SDKMessageProcessingStep = 92;

    /// <summary>
    /// SDK Message Processing Step Image (93).
    /// </summary>
    public const int SDKMessageProcessingStepImage = 93;

    /// <summary>
    /// Service Endpoint (95).
    /// </summary>
    public const int ServiceEndpoint = 95;

    /// <summary>
    /// Routing Rule (150).
    /// </summary>
    public const int RoutingRule = 150;

    /// <summary>
    /// Routing Rule Item (151).
    /// </summary>
    public const int RoutingRuleItem = 151;

    /// <summary>
    /// SLA (152).
    /// </summary>
    public const int SLA = 152;

    /// <summary>
    /// SLA Item (153).
    /// </summary>
    public const int SLAItem = 153;

    /// <summary>
    /// Convert Rule (154).
    /// </summary>
    public const int ConvertRule = 154;

    /// <summary>
    /// Convert Rule Item (155).
    /// </summary>
    public const int ConvertRuleItem = 155;

    /// <summary>
    /// Mobile Offline Profile (161).
    /// </summary>
    public const int MobileOfflineProfile = 161;

    /// <summary>
    /// Mobile Offline Profile Item (162).
    /// </summary>
    public const int MobileOfflineProfileItem = 162;

    /// <summary>
    /// Similarity Rule (165).
    /// </summary>
    public const int SimilarityRule = 165;

    /// <summary>
    /// Custom API (300).
    /// </summary>
    public const int CustomAPI = 300;

    /// <summary>
    /// Custom API Request Parameter (301).
    /// </summary>
    public const int CustomAPIRequestParameter = 301;

    /// <summary>
    /// Custom API Response Property (302).
    /// </summary>
    public const int CustomAPIResponseProperty = 302;

    /// <summary>
    /// Plugin Package (303).
    /// </summary>
    public const int PluginPackage = 303;

    /// <summary>
    /// App Module (80).
    /// </summary>
    public const int AppModule = 80;
}
