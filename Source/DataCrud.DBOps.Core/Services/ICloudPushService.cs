using System.Threading.Tasks;

namespace DataCrud.DBOps.Core.Services
{
    public interface ICloudPushService
    {
        Task PushAsync(string filePath, string providerName);
    }
}
