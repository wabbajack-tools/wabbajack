using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.GraphQL;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("graphql")]
    [ApiController]
    public class GraphQL : AControllerBase<GraphQL>
    {
        private SqlService _sql;

        public GraphQL(ILogger<GraphQL> logger, DBContext db, SqlService sql) : base(logger, db)
        {
            _sql = sql;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] GraphQLQuery query)
        {
            var inputs = query.Variables.ToInputs();
            var schema = new Schema {Query = new Query(Db, _sql), Mutation = new Mutation(Db)};

            var result = await new DocumentExecuter().ExecuteAsync(_ =>
            {
                _.Schema = schema;
                _.Query = query.Query;
                _.OperationName = query.OperationName;
                _.Inputs = inputs;
            });

            if (result.Errors?.Count > 0)
            {
                return BadRequest();
            }

            return Ok(result);
        }
    }
}
