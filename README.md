# plotsol

## Azure Blob Storage setup
1. Sign into Azure and either reuse an existing resource group or create one:
   ```bash
   az login
   az group create --name plotsol-rg --location eastus
   ```
2. Create a storage account with general-purpose v2 and standard redundancy:
   ```bash
   az storage account create \
     --name plotsolstorage<suffix> \
     --resource-group plotsol-rg \
     --location eastus \
     --sku Standard_LRS \
     --kind StorageV2
   ```
3. Provision the PDF container (default name `pdf-uploads` is used by the app):
   ```bash
   az storage container create \
     --account-name plotsolstorage<suffix> \
     --name pdf-uploads \
     --public-access off
   ```
4. Copy the storage account connection string and store it securely. You can grab it from the Azure portal or run:
   ```bash
   az storage account show-connection-string \
     --resource-group plotsol-rg \
     --name plotsolstorage<suffix> \
     --output tsv
   ```
5. Inject the connection string and container name into your environment (for local development, add them to `api/.env` or your secret store):
   ```env
   BlobStorage__ConnectionString=<your connection string>
   BlobStorage__ContainerName=pdf-uploads
   ```
6. Make sure your `.env` also still has the MongoDB and JWT keys. Metadata for every file you upload is written to the MongoDB `files` collection (`MongoDb__FilesCollectionName`), while the binary lives in Azure Blob Storage.

## How the feature works
- After logging in, the left explorer pane exposes an **Upload PDF** button. Choosing a PDF streams it to the `/files/upload` endpoint.
- The backend enforces PDF content, uploads the stream to Azure Blob Storage, and captures metadata (user, file name, size, blob URI, timestamp) in MongoDB for auditing.
- The right pane greets the user, mirrors the upload status, and keeps navigation simple until more files are added.

## Next steps
1. Deploy a MongoDB Atlas cluster (if you haven't yet) and point `MongoDb__ConnectionString` at it.
2. Secure the storage account with private endpoints / firewall rules if you're deploying to production.
3. Consider extending the explorer pane to list uploaded files once metadata is available.
