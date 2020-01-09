using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wabbajack.BuildServer.GraphQL
{
    public class GraphQLQuery
    {

        public string OperationName { get; set; }

        public string NamedQuery { get; set; }
        public string Query { get; set; }
        public JObject Variables { get; set; }
    }
}
