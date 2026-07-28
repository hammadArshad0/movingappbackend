using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.fleet
{
    public class VehicleController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Vehicle?companyId=1&status=Available
        [HttpGet]
        [Route("api/Vehicle")]
        public IHttpActionResult GetAll(int companyId, string status = null)
        {
            try
            {
                var query = db.Vehicles.Where(v => v.CompanyId == companyId);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(v => v.Status == status);

                var vehicles = query
                                .OrderBy(v => v.VehicleNumber)
                                .Select(v => new
                                {
                                    v.Id,
                                    v.VehicleNumber,
                                    v.CapacityWeight,
                                    v.CapacityVolume,
                                    v.Status
                                })
                                .ToList();

                return Ok(vehicles);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Vehicle/5
        [HttpGet]
        [Route("api/Vehicle/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == id);

                if (vehicle == null)
                    return NotFound();

                return Ok(vehicle);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Vehicle/conflicts?fromDate=2026-01-01&toDate=2026-01-05
        // mySQL task: Vehicles with scheduling conflicts (double-booked in overlapping date range)
        [HttpGet]
        [Route("api/Vehicle/conflicts")]
        public IHttpActionResult GetSchedulingConflicts(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var conflicts = (from a in db.MoveAssignments
                                 join m in db.Moves on a.MoveId equals m.Id
                                 where m.PlannedStartDate < toDate && m.PlannedEndDate > fromDate
                                 group new { a, m } by a.VehicleId into g
                                 where g.Count() > 1
                                 select new
                                 {
                                     VehicleId = g.Key,
                                     VehicleNumber = db.Vehicles.Where(v => v.Id == g.Key).Select(v => v.VehicleNumber).FirstOrDefault(),
                                     ConflictingMoves = g.Select(x => new
                                     {
                                         x.m.Id,
                                         x.m.PlannedStartDate,
                                         x.m.PlannedEndDate
                                     }).ToList()
                                 })
                                 .ToList();

                return Ok(conflicts);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Vehicle
        [HttpPost]
        [Route("api/Vehicle")]
        public IHttpActionResult Create([FromBody] VehicleDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                if (string.IsNullOrWhiteSpace(model.VehicleNumber))
                    return BadRequest("VehicleNumber is required.");

                var duplicateNumber = db.Vehicles.Any(v => v.VehicleNumber == model.VehicleNumber);
                if (duplicateNumber)
                    return BadRequest("A vehicle with this number already exists.");

                var vehicle = new Vehicle
                {
                    CompanyId = model.CompanyId,
                    VehicleNumber = model.VehicleNumber,
                    CapacityWeight = model.CapacityWeight,
                    CapacityVolume = model.CapacityVolume,
                    Status = "Available"
                };

                db.Vehicles.Add(vehicle);
                db.SaveChanges();

                return Ok(new { message = "Vehicle created successfully.", id = vehicle.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Vehicle/5
        [HttpPut]
        [Route("api/Vehicle/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] VehicleDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == id);

                if (vehicle == null)
                    return NotFound();

                vehicle.VehicleNumber = model.VehicleNumber;
                vehicle.CapacityWeight = model.CapacityWeight;
                vehicle.CapacityVolume = model.CapacityVolume;

                db.SaveChanges();

                return Ok(new { message = "Vehicle updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Vehicle/5/status
        [HttpPut]
        [Route("api/Vehicle/{id:int}/status")]
        public IHttpActionResult ChangeStatus(int id, [FromBody] ChangeVehicleStatusDto model)
        {
            try
            {
                var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == id);

                if (vehicle == null)
                    return NotFound();

                vehicle.Status = model.Status;
                db.SaveChanges();

                return Ok(new { message = "Vehicle status updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Vehicle/5
        [HttpDelete]
        [Route("api/Vehicle/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == id);

                if (vehicle == null)
                    return NotFound();

                var hasAssignments = db.MoveAssignments.Any(a => a.VehicleId == id);
                if (hasAssignments)
                    return BadRequest("Cannot delete vehicle with existing move assignments.");

                db.Vehicles.Remove(vehicle);
                db.SaveChanges();

                return Ok(new { message = "Vehicle deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class VehicleDto
    {
        public int CompanyId { get; set; }
        public string VehicleNumber { get; set; }
        public decimal? CapacityWeight { get; set; }
        public decimal? CapacityVolume { get; set; }
    }

    public class ChangeVehicleStatusDto
    {
        public string Status { get; set; }
    }
}