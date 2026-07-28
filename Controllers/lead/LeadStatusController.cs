using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.lead
{
    public class LeadStatusController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/LeadStatus
        [HttpGet]
        [Route("api/LeadStatus")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var statuses = db.LeadStatuses
                                 .OrderBy(x => x.Id)
                                 .Select(x => new
                                 {
                                     x.Id,
                                     x.Name
                                 })
                                 .ToList();

                return Ok(statuses);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/LeadStatus/5
        [HttpGet]
        [Route("api/LeadStatus/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var status = db.LeadStatuses
                               .Where(x => x.Id == id)
                               .Select(x => new
                               {
                                   x.Id,
                                   x.Name
                               })
                               .FirstOrDefault();

                if (status == null)
                    return NotFound();

                return Ok(status);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}