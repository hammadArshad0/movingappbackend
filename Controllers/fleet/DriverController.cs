using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.fleet
{
    public class DriverController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Driver
        [HttpGet]
        [Route("api/Driver")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var drivers = db.Drivers
                                 .Select(d => new
                                 {
                                     d.Id,
                                     d.UserId,
                                     DriverName = db.Users.Where(u => u.Id == d.UserId)
                                                   .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                                     d.LicenseNumber
                                 })
                                 .ToList();

                return Ok(drivers);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Driver/5
        [HttpGet]
        [Route("api/Driver/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var driver = db.Drivers.FirstOrDefault(d => d.Id == id);

                if (driver == null)
                    return NotFound();

                return Ok(driver);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Driver/5/availability?fromDate=...&toDate=...
        // Driver overlapping move check karne ke liye (assignment se pehle UI se call hoga)
        [HttpGet]
        [Route("api/Driver/{id:int}/availability")]
        public IHttpActionResult CheckAvailability(int id, DateTime fromDate, DateTime toDate)
        {
            try
            {
                var isBusy = (from a in db.MoveAssignments
                              join m in db.Moves on a.MoveId equals m.Id
                              where a.DriverId == id
                                    && m.Status != "Cancelled"
                                    && m.PlannedStartDate < toDate
                                    && m.PlannedEndDate > fromDate
                              select a.Id)
                             .Any();

                return Ok(new { available = !isBusy });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Driver
        [HttpPost]
        [Route("api/Driver")]
        public IHttpActionResult Create([FromBody] DriverDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var userExists = db.Users.Any(u => u.Id == model.UserId && u.IsDeleted == false);
                if (!userExists)
                    return BadRequest("Invalid UserId.");

                var alreadyDriver = db.Drivers.Any(d => d.UserId == model.UserId);
                if (alreadyDriver)
                    return BadRequest("This user is already registered as a driver.");

                var driver = new Driver
                {
                    UserId = model.UserId,
                    LicenseNumber = model.LicenseNumber
                };

                db.Drivers.Add(driver);
                db.SaveChanges();

                return Ok(new { message = "Driver created successfully.", id = driver.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Driver/5
        [HttpPut]
        [Route("api/Driver/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] DriverDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var driver = db.Drivers.FirstOrDefault(d => d.Id == id);

                if (driver == null)
                    return NotFound();

                driver.LicenseNumber = model.LicenseNumber;
                db.SaveChanges();

                return Ok(new { message = "Driver updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Driver/5
        [HttpDelete]
        [Route("api/Driver/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var driver = db.Drivers.FirstOrDefault(d => d.Id == id);

                if (driver == null)
                    return NotFound();

                var hasAssignments = db.MoveAssignments.Any(a => a.DriverId == id);
                if (hasAssignments)
                    return BadRequest("Cannot delete driver with existing move assignments.");

                db.Drivers.Remove(driver);
                db.SaveChanges();

                return Ok(new { message = "Driver deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class DriverDto
    {
        public int UserId { get; set; }
        public string LicenseNumber { get; set; }
    }
}