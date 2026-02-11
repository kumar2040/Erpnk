namespace NkplmErp.Shared.Constants;

public static class Permissions
{
    public static class Users
    {
        public const string View = "Permissions.Users.View";
        public const string Create = "Permissions.Users.Create";
        public const string Edit = "Permissions.Users.Edit";
        public const string Delete = "Permissions.Users.Delete";
    }

    public static class Roles
    {
        public const string View = "Permissions.Roles.View";
        public const string Manage = "Permissions.Roles.Manage";
    }

    public static class Invoices
    {
        public const string View = "Permissions.Invoices.View";
        public const string Create = "Permissions.Invoices.Create";
        public const string Edit = "Permissions.Invoices.Edit";
        public const string Delete = "Permissions.Invoices.Delete";
        public const string Approve = "Permissions.Invoices.Approve";
    }

    public static class Audit
    {
        public const string View = "Permissions.Audit.View";
    }
}
