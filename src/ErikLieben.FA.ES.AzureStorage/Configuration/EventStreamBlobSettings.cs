namespace ErikLieben.FA.ES.AzureStorage.Configuration;

public record EventStreamBlobSettings
{ 
    public string DefaultDataStore { get; init; }
    
    public string DefaultDocumentStore { get; init; }
    
    public string DefaultSnapShotStore { get; init; }
    
    public string DefaultDocumentTagStore { get; init; }
    
    public bool AutoCreateContainer { get; init; }
    
    public bool EnableStreamChunks { get; init; }
    
    public int DefaultChunkSize { get; init; }
    
    public string DefaultDocumentContainerName { get; init; }
    
    public EventStreamBlobSettings(
        string defaultDataStore,
        string defaultDocumentStore = null!,
        string defaultSnapShotStore = null!,
        string defaultDocumentTagStore = null!,
        bool autoCreateContainer = true,
        bool enableStreamChunks = false,
        int defaultChunkSize = 1000,
        string defaultDocumentContainerName = "object-document-store")
    {
        ArgumentNullException.ThrowIfNull(defaultDataStore);
        
        DefaultDataStore = defaultDataStore;
        DefaultDocumentStore = defaultDocumentStore ?? DefaultDataStore;
        DefaultSnapShotStore = defaultSnapShotStore ?? DefaultDataStore;
        DefaultDocumentTagStore = defaultDocumentTagStore ?? DefaultDataStore;
        DefaultDocumentContainerName = defaultDocumentContainerName;
        AutoCreateContainer = autoCreateContainer;
        EnableStreamChunks = enableStreamChunks;
        DefaultChunkSize = defaultChunkSize;
    }

}