using System;
using PFound.GameDataStore.Storage;
using PFound.GameDataStore.Entries;

namespace PFound.DataStorage.Test
{
    [DataStoreConfig(Initialization = DataStoreDatabaseInitializationMethod.InitializeAtStart)]
    public class BookDataStore: DataStoreClass<BookDataStore>
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime Year { get; set; }
        public string Genre { get; set; }
        public string Publisher { get; set; }
        public string ISBN { get; set; }
        public int Pages { get; set; }
        public string Language { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        
        public Entity MetaData { get; set; }
        public WeekDay ScheduledReadingDay { get; set; }
    }
}