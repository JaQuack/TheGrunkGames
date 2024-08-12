using Azure.Data.Tables;
using System;

namespace TheGrunkGames.Services
{
    public class StorageService
    {


        public StorageService()
        {
            var uri = Environment.GetEnvironmentVariable("Storage.Uri");
            var account = Environment.GetEnvironmentVariable("Storage.Account");
            var key = Environment.GetEnvironmentVariable("Storage.Key");

            var serviceClient = new TableServiceClient(
                new Uri(uri),
                new TableSharedKeyCredential(account, key));
        }
    }
}
