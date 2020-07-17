using System;
using Microsoft.AspNetCore.Mvc;

namespace ImageViewer.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class HealthcheckController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
    }
}
