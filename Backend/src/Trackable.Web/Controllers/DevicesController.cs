﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trackable.Common;
using Trackable.Models;
using Trackable.Services;
using Trackable.Web.Auth;

namespace Trackable.Web.Controllers
{
    [Route("api/devices")]
    public class DevicesController : ControllerBase
    {
        private readonly ITrackingDeviceService deviceService;
        private readonly ITrackingPointService pointService;
        private readonly IGeoFenceService geoFenceService;
        private readonly IHubContext<DynamicHub> deviceAdditionHubContext;
        private readonly IConfiguration configuration;
        private readonly ITokenService tokenService;

        public DevicesController(
            ITrackingDeviceService deviceService,
            ITrackingPointService pointService,
            IGeoFenceService geoFenceService,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IHubContext<DynamicHub> deviceAdditionHubContext,
            ITokenService tokenService)
            : base(loggerFactory)
        {
            this.deviceService = deviceService.ThrowIfNull(nameof(deviceService));
            this.pointService = pointService.ThrowIfNull(nameof(pointService));
            this.geoFenceService = geoFenceService.ThrowIfNull(nameof(geoFenceService));
            this.deviceAdditionHubContext = deviceAdditionHubContext;
            this.tokenService = tokenService;
            this.configuration = configuration;
        }

        // GET api/devices
        [HttpGet]
        public async Task<IEnumerable<TrackingDevice>> Get(
            [FromQuery] string tags = null,
            [FromQuery] bool includesAllTags = false,
            [FromQuery] string name = null)
        {
            if (string.IsNullOrEmpty(tags) && string.IsNullOrEmpty(name))
            {
                return await this.deviceService.ListAsync();
            }

            IEnumerable<TrackingDevice> taggedResults = null;
            if (!string.IsNullOrEmpty(tags))
            {
                var tagsArray = tags.Split(',');
                if (includesAllTags)
                {
                    taggedResults = await this.deviceService.FindContainingAllTagsAsync(tagsArray);
                }
                else
                {
                    taggedResults = await this.deviceService.FindContainingAnyTagsAsync(tagsArray);
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                return taggedResults;
            }

            var resultsByName = await this.deviceService.FindByNameAsync(name);
            if (taggedResults == null)
            {
                return resultsByName;
            }

            return taggedResults.Where(d => resultsByName.Select(r => r.Id).Contains(d.Id));
        }

        // GET api/devices/5
        [HttpGet("{id}")]
        public async Task<TrackingDevice> Get(string id)
        {
            return await this.deviceService.GetAsync(id);
        }

        // Post api/devices/5/token
        [HttpPost("{id}/token")]
        [Authorize(UserRoles.Administrator)]
        public async Task<IActionResult> GetDeviceToken(string id)
        {
            var device = await this.deviceService.GetAsync(id);

            return Json(await this.tokenService.GetLongLivedDeviceToken(device, false));
        }

        // GET api/devices/all/positions
        [HttpGet("all/positions")]
        public async Task<IDictionary<string, TrackingPoint>> GetLatestPositions(string id)
        {
            return await this.deviceService.GetDevicesLatestPositions();
        }

        // GET api/devices/5/points
        [HttpGet("{id}/points")]
        public async Task<IEnumerable<TrackingPoint>> GetPoints(string id)
        {
            return await this.pointService.GetByDeviceIdAsync(id);
        }

        // POST api/devices
        [HttpPost]
        [Authorize(UserRoles.DeviceRegistration)]
        public async Task<IActionResult> Post([FromBody]TrackingDevice device, [FromQuery]string nonce = null)
        {
            TrackingDevice response = await this.deviceService.AddOrUpdateDeviceAsync(device);

            await this.deviceAdditionHubContext.Clients.All.InvokeAsync("DeviceAdded", nonce);

            return Json(await this.tokenService.GetLongLivedDeviceToken(response, false));
        }

        // POST api/devices
        [HttpPost("batch")]
        [Authorize(UserRoles.Administrator)]
        public async Task<IActionResult> Post([FromBody]TrackingDevice[] devices)
        {
            var addedDevices = await this.deviceService.AddAsync(devices);

            var tokens = new List<string>();
            foreach (var device in addedDevices)
            {
                tokens.Add(await this.tokenService.GetLongLivedDeviceToken(device, false));
            }

            return Json(tokens);
        }

        // POST api/devices/5/points
        [HttpPost("{id}/points")]
        [Authorize(UserRoles.TrackingDevice)]
        public async Task<IActionResult> PostPointsToDevice(string id, [FromBody]TrackingPoint[] points)
        {
            var subject = ClaimsReader.ReadSubject(this.User);
            var audience = ClaimsReader.ReadAudience(this.User);

            if (audience == JwtAuthConstants.DeviceAudience && id != subject)
            {
                return Forbid();
            }

            points.ForEach((point) => point.TrackingDeviceId = id);
            var addedPoints = await this.pointService.AddAsync(points);
            await this.geoFenceService.HandlePoints(addedPoints.First().AssetId, addedPoints.ToArray());

            return Ok();
        }

        // PUT api/devices/5
        [HttpPut("{id}")]
        [Authorize(UserRoles.Administrator)]
        public async Task<TrackingDevice> Put(string id, [FromBody]TrackingDevice device)
        {
            return await this.deviceService.UpdateAsync(id, device);
        }

        // DELETE api/devices/5
        [HttpDelete("{id}")]
        [Authorize(UserRoles.Administrator)]
        public async Task Delete(string id)
        {
            var device = await this.deviceService.GetAsync(id);

            if (device != null)
            {
                await this.tokenService.DisableDeviceTokens(device);
                await this.deviceService.DeleteAsync(device.Id);
            }
        }

        // GET api/devices/qrcode
        [HttpGet("qrcode")]
        [Authorize(UserRoles.Administrator)]
        public IActionResult GetProvisioningQrCode(string nonce = null, int height = 300, int width = 300, int margin = 0)
        {
            string queryParams = string.Empty;
            if (!String.IsNullOrEmpty(nonce))
            {
                queryParams = $"?nonce={nonce}";
            }

            var data = new PhoneClientData
            {
                BaseUrl = $"{this.Request.Scheme}://{this.Request.Host}",
                RegistrationUrl = $"{this.Request.Scheme}://{this.Request.Host}/api/devices{queryParams}",
                RegistrationToken = this.tokenService.GetShortLivedDeviceRegistrationToken()
            };

            return File(
                this.deviceService.GetDeviceProvisioningQrCode(
                    data,
                    height,
                    width,
                    margin),
                "image/png");
        }
    }
}
