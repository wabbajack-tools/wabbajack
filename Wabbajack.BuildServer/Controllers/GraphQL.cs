using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Language.AST;
using GraphQL.Types;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Wabbajack.BuildServer.GraphQL;
using Wabbajack.BuildServer.Models;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("graphql")]
    [ApiController]
    public class GraphQL : AControllerBase<GraphQL>
    {
        public GraphQL(ILogger<GraphQL> logger, DBContext db) : base(logger, db)
        {
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] GraphQLQuery query)
        {
            var inputs = query.Variables.ToInputs();
            var schema = new Schema
            {
                Query = new Query(Db),
                Mutation = new Mutation(Db)
            };
            
            var result = await new DocumentExecuter().ExecuteAsync(_ =>
            {
                _.Schema = schema;
                _.Query = query.Query;
                _.OperationName = query.OperationName;
                _.Inputs = inputs;
            });
            
            if(result.Errors?.Count > 0)
            {
                return BadRequest();
            }

            return Ok(result);
        }
       
    }
}
