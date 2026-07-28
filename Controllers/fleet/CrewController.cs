using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.fleet
{
    [RoutePrefix("api/Crew")]
    public class CrewController : ApiController
    {
        private readonly MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET: api/Crew?companyId=1
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAll(int companyId)
        {
            try
            {
                var crews = db.Crews
                    .Where(c => c.CompanyId == companyId)
                    .ToList()
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        MemberCount = c.Users.Count()
                    })
                    .ToList();

                return Ok(crews);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/Crew/5
        [HttpGet]
        [Route("{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var crew = db.Crews.FirstOrDefault(c => c.Id == id);

                if (crew == null)
                    return NotFound();

                var members = crew.Users
                    .Select(u => new
                    {
                        u.Id,
                        Name = (u.FirstName ?? "") + " " + (u.LastName ?? ""),
                        u.Email
                    })
                    .ToList();

                return Ok(new
                {
                    crew.Id,
                    crew.Name,
                    Members = members
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST: api/Crew
        [HttpPost]
        [Route("")]
        public IHttpActionResult Create([FromBody] CrewDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                if (string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest("Crew name is required.");

                var crew = new Crew
                {
                    CompanyId = model.CompanyId,
                    Name = model.Name
                };

                db.Crews.Add(crew);
                db.SaveChanges();

                return Ok(new
                {
                    message = "Crew created successfully.",
                    crew.Id
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT: api/Crew/5
        [HttpPut]
        [Route("{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] CrewDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var crew = db.Crews.FirstOrDefault(c => c.Id == id);

                if (crew == null)
                    return NotFound();

                crew.Name = model.Name;

                db.SaveChanges();

                return Ok(new
                {
                    message = "Crew updated successfully."
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST: api/Crew/5/member
        [HttpPost]
        [Route("{id:int}/member")]
        public IHttpActionResult AddMember(int id, [FromBody] AddCrewMemberDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var crew = db.Crews.FirstOrDefault(c => c.Id == id);

                if (crew == null)
                    return NotFound();

                var user = db.Users.FirstOrDefault(u =>
                    u.Id == model.UserId &&
                    u.IsDeleted == false);

                if (user == null)
                    return BadRequest("Invalid User.");

                if (crew.Users.Any(u => u.Id == model.UserId))
                    return BadRequest("User already exists in this crew.");

                crew.Users.Add(user);

                db.SaveChanges();

                return Ok(new
                {
                    message = "Member added successfully."
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE: api/Crew/5/member/10
        [HttpDelete]
        [Route("{id:int}/member/{userId:int}")]
        public IHttpActionResult RemoveMember(int id, int userId)
        {
            try
            {
                var crew = db.Crews.FirstOrDefault(c => c.Id == id);

                if (crew == null)
                    return NotFound();

                var user = crew.Users.FirstOrDefault(u => u.Id == userId);

                if (user == null)
                    return NotFound();

                crew.Users.Remove(user);

                db.SaveChanges();

                return Ok(new
                {
                    message = "Member removed successfully."
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE: api/Crew/5
        [HttpDelete]
        [Route("{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var crew = db.Crews.FirstOrDefault(c => c.Id == id);

                if (crew == null)
                    return NotFound();

                bool hasAssignments = db.MoveAssignments.Any(a => a.CrewId == id);

                if (hasAssignments)
                    return BadRequest("Cannot delete crew with existing move assignments.");

                crew.Users.Clear();

                db.Crews.Remove(crew);

                db.SaveChanges();

                return Ok(new
                {
                    message = "Crew deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class CrewDto
    {
        public int CompanyId { get; set; }
        public string Name { get; set; }
    }

    public class AddCrewMemberDto
    {
        public int UserId { get; set; }
    }
}