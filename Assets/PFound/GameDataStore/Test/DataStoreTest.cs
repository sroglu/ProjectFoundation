using System;
using PFound.GameDataStore.Storage;
using PFound.GameDataStore.Entries;
using UnityEngine;

namespace PFound.DataStorage.Test
{
    public class DataStoreTest : MonoBehaviour
    {
        [SerializeField] private DataDefinitionConfig dataDefinitionConfig;
        
        [SerializeField] private Entry anyKindData;
        private BookDataStore bookDataStore => BookDataStore.Instance;

        public Entry AnyKindData
        {
            get => anyKindData;
            set => anyKindData = value;
        }
        public BookDataStore BookData => bookDataStore;

        private EntryManager _entryManager;
        private DataStoreManager _dataStoreManager;
        private void Awake()
        {
            _entryManager = new EntryManager(dataDefinitionConfig);
            _dataStoreManager = new DataStoreManager();
            
            BookDataStore.Initialize();
            BookDataStore.Instance.BookId = 1;
            BookDataStore.Instance.Title = "The Hobbit";
            BookDataStore.Instance.Author = "J.R.R. Tolkien";
            BookDataStore.Instance.Year = new DateTime(1937, 9, 21);
            BookDataStore.Instance.Genre = "Fantasy";
            BookDataStore.Instance.Publisher = "George Allen & Unwin";
            BookDataStore.Instance.ISBN = "978-0-395-08355-1";
            BookDataStore.Instance.Pages = 310;
            BookDataStore.Instance.Language = "English";
            BookDataStore.Instance.Description = "The Hobbit, or There and Back Again is a children's fantasy novel by English author J. R. R. Tolkien. It was published on 21 September 1937 to wide critical acclaim, being nominated for the Carnegie Medal and awarded a prize from the New York Herald Tribune for best juvenile fiction. The book remains popular and is recognized as a classic in children's literature.";
            BookDataStore.Instance.ImageUrl = "https://upload.wikimedia.org/wikipedia/en/3/30/Hobbit_cover.JPG";
            BookDataStore.Instance.MetaData = new Entity(EntityType.Book, EntityLevel.Zero);
            BookDataStore.Instance.ScheduledReadingDay = WeekDay.Monday;
        }
        
        private void Start()
        {
            Debug.Log(BookDataStore.Instance.Title);
            Debug.Log(BookDataStore.Instance.Author);
            Debug.Log(BookDataStore.Instance.Year);
            Debug.Log(BookDataStore.Instance.Genre);
            Debug.Log(BookDataStore.Instance.Publisher);
            Debug.Log(BookDataStore.Instance.ISBN);
            Debug.Log(BookDataStore.Instance.Pages);
            Debug.Log(BookDataStore.Instance.Language);
            Debug.Log(BookDataStore.Instance.Description);
            Debug.Log(BookDataStore.Instance.ImageUrl);
            Debug.Log(BookDataStore.Instance.MetaData);
            Debug.Log(BookDataStore.Instance.ScheduledReadingDay);
            
        }
    }
}