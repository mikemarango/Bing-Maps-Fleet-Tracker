﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trackable.Common;
using Trackable.Models;
using Trackable.Services;
using Trackable.Web.DTOs;

namespace Trackable.Web.Controllers
{
    [Route("api/geofences")]
    [Authorize(UserRoles.Administrator)]
    public class GeoFencesController : ControllerBase
    {
        private readonly IGeoFenceService geoFenceService;
        private readonly IMapper dtoMapper;

        public GeoFencesController(
            IGeoFenceService geoFenceService,
            ILoggerFactory loggerFactory,
            IMapper dtoMapper)
            : base(loggerFactory)
        {
            this.geoFenceService = geoFenceService;
            this.dtoMapper = dtoMapper;
        }

        // GET api/geofences
        [HttpGet]
        public async Task<IEnumerable<GeoFenceDto>> Get(
            [FromQuery] string tags = null,
            [FromQuery] bool includesAllTags = false,
            [FromQuery] string name = null)
        {
            if (string.IsNullOrEmpty(tags) && string.IsNullOrEmpty(name))
            {
                var results = await this.geoFenceService.ListAsync();
                return this.dtoMapper.Map<IEnumerable<GeoFenceDto>>(results);
            }

            if (!string.IsNullOrEmpty(name))
            {
                var results = await this.geoFenceService.FindByNameAsync(name);
                return this.dtoMapper.Map<IEnumerable<GeoFenceDto>>(results);
            }

            var tagsArray = tags.Split(',');
            if (includesAllTags)
            {
                var results = await this.geoFenceService.FindContainingAllTagsAsync(tagsArray);
                return this.dtoMapper.Map<IEnumerable<GeoFenceDto>>(results);
            }
            else
            {
                var results = await this.geoFenceService.FindContainingAnyTagsAsync(tagsArray);
                return this.dtoMapper.Map<IEnumerable<GeoFenceDto>>(results);
            }
        }

        // GET api/geofences/5
        [HttpGet("{id}")]
        public async Task<GeoFenceDto> Get(int id)
        {
            var result = await this.geoFenceService.GetAsync(id);

            return this.dtoMapper.Map<GeoFenceDto>(result);
        }

        // POST api/geofences
        [HttpPost]
        public async Task<GeoFenceDto> Post([FromBody]GeoFenceDto geoFence)
        {
            var model = this.dtoMapper.Map<GeoFence>(geoFence);

            var result = await this.geoFenceService.AddAsync(model);

            return this.dtoMapper.Map<GeoFenceDto>(result);
        }

        // POST api/geofences/batch
        [HttpPost("batch")]
        public async Task<IEnumerable<GeoFenceDto>> PostBatch([FromBody]GeoFenceDto[] geoFences)
        {
            var models = this.dtoMapper.Map<GeoFence[]>(geoFences);

            var results = await this.geoFenceService.AddAsync(models);

            return this.dtoMapper.Map<IEnumerable<GeoFenceDto>>(results);
        }

        [HttpPut("{id}")]
        public async Task<GeoFenceDto> Put(int id, [FromBody]GeoFenceDto geoFence)
        {
            var model = this.dtoMapper.Map<GeoFence>(geoFence);

            var result =  await this.geoFenceService.UpdateAsync(id, model);

            return this.dtoMapper.Map<GeoFenceDto>(result);
        }

        // DELETE api/geofences/5
        [HttpDelete("{id}")]
        public async Task Delete(int id)
        {
            await this.geoFenceService.DeleteAsync(id);
        }
    }
}
