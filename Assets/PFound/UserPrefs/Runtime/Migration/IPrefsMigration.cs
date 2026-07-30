namespace PFound.UserPrefs
{
    public interface IPrefsMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        void Apply(MigrationContext context);
    }
}
