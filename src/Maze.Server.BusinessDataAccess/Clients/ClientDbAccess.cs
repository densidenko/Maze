using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Maze.Server.Data.EfClasses;
using Maze.Server.Data.EfCode;

namespace Maze.Server.BusinessDataAccess.Clients
{
    public interface IClientDbAccess
    {
        Task<Client> FindClientByHardwareId(string hardwareId);
        void AddClient(Client client);
    }

    public class ClientDbAccess : IClientDbAccess
    {
        private readonly AppDbContext _context;

        public ClientDbAccess(AppDbContext context)
        {
            _context = context;
        }

        public Task<Client> FindClientByHardwareId(string hardwareId)
        {
            return _context.Clients.Include(x => x.ClientSessions).FirstOrDefaultAsync(x => x.HardwareId == hardwareId);
        }

        public void AddClient(Client client)
        {
            _context.Add(client);
        }
    }
}