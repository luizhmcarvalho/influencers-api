﻿using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using Action.Models;
using Action.Models.Scrap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Action.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class ScrapSourcesController : Controller
    {
        private readonly HtmlEncoder _htmlEncoder;
        private readonly ApplicationDbContext _dbContext;

        public ScrapSourcesController(HtmlEncoder htmlEncoder, ApplicationDbContext dbContext = null)
        {
            _dbContext = dbContext;
            _htmlEncoder = htmlEncoder;
        }

        // GET: api/values
        [HttpGet]
        public IEnumerable<ScrapSource> Get()
        {
            if (_dbContext == null)
            {
                return null;
            }
            else
            {
                return _dbContext.ScrapSources.ToList();
            }
        }

        // POST api/values
        [HttpPost]
        public ScrapSource Post([FromBody]ScrapSource model)
        {
            if (_dbContext == null)
            {
                return null;
            }
            else
            {
                _dbContext.ScrapSources.Add(model);
                _dbContext.SaveChanges();
                return model;
            }
        }
        
        // PUT api/values
        [HttpPut]
        public ScrapSource Put([FromBody]ScrapSource model)
        {
            if (_dbContext == null)
            {
                return null;
            }
            else
            {
                _dbContext.Entry(model).State = EntityState.Modified;
                _dbContext.SaveChanges();
                return model;
            }
        }
        
        // DELETE api/values
        [HttpPut]
        public ScrapSource Delete([FromBody]ScrapSource model)
        {
            if (_dbContext == null)
            {
                return null;
            }
            else
            {
                var item =_dbContext.ScrapSources.Find(model.Id);
                _dbContext.ScrapSources.Remove(item);
                _dbContext.SaveChanges();
                return model;
            }
        }
        
        
    }
}
