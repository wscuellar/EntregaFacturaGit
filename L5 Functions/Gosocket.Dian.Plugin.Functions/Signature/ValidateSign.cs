using Gosocket.Dian.Domain.Entity;
using Gosocket.Dian.Infrastructure;
using Gosocket.Dian.Plugin.Functions.Common;
using Gosocket.Dian.Plugin.Functions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Gosocket.Dian.Plugin.Functions.Signature
{
    public static class ValidateSign
    {
        private static readonly TableManager tableManagerGlobalLogger = new TableManager("GlobalLogger");

        [FunctionName("ValidateSign")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // Get request body
            var data = await req.Content.ReadAsAsync<RequestObject>();

            if (data == null || string.IsNullOrEmpty(data.TrackId))
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a trackId on the query string or in the request body");

            var trackId = data.TrackId;

            try
            {
                var validateResponses = await ValidatorEngine.Instance.StartSignValidationAsync(trackId);

                // Only for test, need removed in the future.
                try
                {
                    if (validateResponses.Any(_ => _.ErrorCode == "ZD05" && !_.IsValid))
                    {
                        var validation = validateResponses.FirstOrDefault(_ => _.ErrorCode == "ZD05" && !_.IsValid);
                        var logger = new GlobalLogger("ZD05", trackId) { Action = "ValidateSign", Message = validation.ErrorMessage };
                        await tableManagerGlobalLogger.InsertOrUpdateAsync(logger);
                    }
                }
                catch { }

                return req.CreateResponse(HttpStatusCode.OK, validateResponses);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message + "_________" + ex.StackTrace + "_________" + ex.Source, ex);
                var logger = new GlobalLogger($"SIGNPLGNS-{DateTime.UtcNow.ToString("yyyyMMdd")}", trackId) { Message = ex.Message, StackTrace = ex.StackTrace };
                await tableManagerGlobalLogger.InsertOrUpdateAsync(logger);

                var validateResponses = new List<ValidateListResponse>
                {
                    new ValidateListResponse
                    {
                        IsValid = false,
                        Mandatory = true,
                        ErrorCode = "SIGNPLGNS",
                        ErrorMessage = $"No se pudo validar firma."
                    }
                };
                return req.CreateResponse(HttpStatusCode.InternalServerError, validateResponses);
            }
        }

        public class RequestObject
        {
            [JsonProperty(PropertyName = "trackId")]
            public string TrackId { get; set; }
        }
    }
}