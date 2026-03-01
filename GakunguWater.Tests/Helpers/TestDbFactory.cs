using GakunguWater.Data;

namespace GakunguWater.Tests.Helpers;

/// <summary>
/// Creates a fresh in-memory SQLite DatabaseService for each test.
/// Use a unique DB name per test to guarantee full isolation.
/// </summary>
public static class TestDbFactory
{
    private static int _counter = 0;

    public static DatabaseService Create()
    {
        int id = System.Threading.Interlocked.Increment(ref _counter);
        // "mode=memory&cache=shared" + unique name keeps the DB alive
        // for the lifetime of the test without touching the filesystem.
        var db = new DatabaseService($"file:testdb_{id}?mode=memory&cache=shared");
        db.Initialize();
        return db;
    }
}
