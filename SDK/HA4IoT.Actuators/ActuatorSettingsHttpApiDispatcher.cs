﻿using System;
using Windows.Data.Json;
using HA4IoT.Networking;

namespace HA4IoT.Actuators
{
    public class ActuatorSettingsHttpApiDispatcher
    {
        private readonly ActuatorSettings _actuatorSettings;
        private readonly IHttpRequestController _httpApiController;

        public ActuatorSettingsHttpApiDispatcher(ActuatorSettings actuatorSettings, IHttpRequestController httpApiController)
        {
            if (actuatorSettings == null) throw new ArgumentNullException(nameof(actuatorSettings));
            if (httpApiController == null) throw new ArgumentNullException(nameof(httpApiController));

            _actuatorSettings = actuatorSettings;
            _httpApiController = httpApiController;
        }
        
        public void ExposeToApi()
        {
            _httpApiController.Handle(HttpMethod.Get, $"actuator/{_actuatorSettings.ActuatorId}/settings").Using(HandleApiGet);
            _httpApiController.Handle(HttpMethod.Post, $"actuator/{_actuatorSettings.ActuatorId}/settings").Using(HandleApiPost);
        }

        private void HandleApiGet(HttpContext httpContext)
        {
            httpContext.Response.Body = new JsonBody(_actuatorSettings.ExportToJsonObject());
        }

        private void HandleApiPost(HttpContext httpContext)
        {
            JsonObject requestBody;
            if (!JsonObject.TryParse(httpContext.Request.Body, out requestBody))
            {
                httpContext.Response.StatusCode = HttpStatusCode.BadRequest;
                return;
            }

            _actuatorSettings.ImportFromJsonObject(requestBody);
        }
    }
}
