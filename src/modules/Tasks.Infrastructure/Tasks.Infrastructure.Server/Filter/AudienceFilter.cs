using System;
using System.Linq;
using System.Threading.Tasks;
using Maze.Server.Connection.Commanding;
using Tasks.Infrastructure.Core.Audience;

namespace Tasks.Infrastructure.Server.Filter
{
    public class AudienceFilter : IClientFilter
    {
        private readonly AudienceCollection _audienceCollection;

        public AudienceFilter(AudienceCollection audienceCollection)
        {
            _audienceCollection = audienceCollection;
        }
        
        public bool IsServerIncluded() => _audienceCollection.IncludesServer;
        public int? Cost { get; } = 0;

        public Task<bool> Invoke(int clientId, IServiceProvider serviceProvider)
        {
            return Task.FromResult(Invoke(clientId));
        }

        public bool Invoke(int clientId)
        {
            if (_audienceCollection.IsAll)
                return true;

            if (_audienceCollection.Any(x => x.Type == CommandTargetType.Client && clientId >= x.From && clientId <= x.To))
                return true;

            //check group
            throw new NotImplementedException();
        }
    }
}