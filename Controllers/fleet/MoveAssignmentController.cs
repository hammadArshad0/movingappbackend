using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.fleet
{
    public class MoveAssignmentController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/MoveAssignment?moveId=1
        [HttpGet]
        [Route("api/MoveAssignment")]
        public IHttpActionResult GetAll(int? moveId = null)
        {
            try
            {
                var query = db.MoveAssignments.AsQueryable();

                if (moveId.HasValue)
                    query = query.Where(a => a.MoveId == moveId.Value);

                var assignments = query
                                   .Select(a => new
                                   {
                                       a.Id,
                                       a.MoveId,
                                       a.VehicleId,
                                       VehicleNumber = db.Vehicles.Where(v => v.Id == a.VehicleId).Select(v => v.VehicleNumber).FirstOrDefault(),
                                       a.DriverId,
                                       a.CrewId
                                   })
                                   .ToList();

                return Ok(assignments);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/MoveAssignment/5
        [HttpGet]
        [Route("api/MoveAssignment/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var assignment = db.MoveAssignments.FirstOrDefault(a => a.Id == id);

                if (assignment == null)
                    return NotFound();

                return Ok(assignment);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/MoveAssignment
        // MANDATORY RULES:
        // 1) Driver cannot be assigned to overlapping moves
        // 2) Vehicle capacity must not be exceeded (survey's TotalWeight/TotalVolume vs Vehicle capacity)
        [HttpPost]
        [Route("api/MoveAssignment")]
        public IHttpActionResult Create([FromBody] MoveAssignmentDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var move = db.Moves.FirstOrDefault(m => m.Id == model.MoveId);
                if (move == null)
                    return BadRequest("Invalid MoveId.");

                if (move.PlannedStartDate == null || move.PlannedEndDate == null)
                    return BadRequest("Move must have PlannedStartDate and PlannedEndDate before assignment.");

                // === RULE 1: Vehicle capacity validation ===
                if (model.VehicleId.HasValue)
                {
                    var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == model.VehicleId.Value);
                    if (vehicle == null)
                        return BadRequest("Invalid VehicleId.");

                    // Survey se load calculate karo (Move -> Quote -> Lead -> Survey chain)
                    var survey = (from q in db.Quotes
                                  join s in db.Surveys on q.LeadId equals s.LeadId
                                  where q.Id == move.QuoteId
                                  select s)
                                 .FirstOrDefault();

                    if (survey != null)
                    {
                        if (survey.TotalWeight > vehicle.CapacityWeight)
                            return BadRequest($"Vehicle capacity exceeded: load weight {survey.TotalWeight} exceeds vehicle capacity {vehicle.CapacityWeight}.");

                        if (survey.TotalVolume > vehicle.CapacityVolume)
                            return BadRequest($"Vehicle capacity exceeded: load volume {survey.TotalVolume} exceeds vehicle capacity {vehicle.CapacityVolume}.");
                    }

                    // Vehicle already kisi overlapping move mein assign to nahi hai
                    var vehicleConflict = (from a in db.MoveAssignments
                                           join m in db.Moves on a.MoveId equals m.Id
                                           where a.VehicleId == model.VehicleId.Value
                                                 && m.Id != move.Id
                                                 && m.Status != "Cancelled"
                                                 && m.PlannedStartDate < move.PlannedEndDate
                                                 && m.PlannedEndDate > move.PlannedStartDate
                                           select a.Id)
                                           .Any();

                    if (vehicleConflict)
                        return BadRequest("This vehicle is already assigned to another move in the same time window.");
                }

                // === RULE 2: Driver cannot be assigned to overlapping moves ===
                if (model.DriverId.HasValue)
                {
                    var driverExists = db.Drivers.Any(d => d.Id == model.DriverId.Value);
                    if (!driverExists)
                        return BadRequest("Invalid DriverId.");

                    var driverConflict = (from a in db.MoveAssignments
                                          join m in db.Moves on a.MoveId equals m.Id
                                          where a.DriverId == model.DriverId.Value
                                                && m.Id != move.Id
                                                && m.Status != "Cancelled"
                                                && m.PlannedStartDate < move.PlannedEndDate
                                                && m.PlannedEndDate > move.PlannedStartDate
                                          select a.Id)
                                          .Any();

                    if (driverConflict)
                        return BadRequest("This driver is already assigned to an overlapping move.");
                }

                if (model.CrewId.HasValue)
                {
                    var crewExists = db.Crews.Any(c => c.Id == model.CrewId.Value);
                    if (!crewExists)
                        return BadRequest("Invalid CrewId.");
                }

                var assignment = new MoveAssignment
                {
                    MoveId = model.MoveId,
                    VehicleId = model.VehicleId,
                    DriverId = model.DriverId,
                    CrewId = model.CrewId
                };

                db.MoveAssignments.Add(assignment);

                // Vehicle status ko "Assigned" mark kar do
                if (model.VehicleId.HasValue)
                {
                    var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == model.VehicleId.Value);
                    if (vehicle != null)
                        vehicle.Status = "Assigned";
                }

                db.SaveChanges();

                return Ok(new { message = "Assignment created successfully.", id = assignment.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/MoveAssignment/5
        [HttpDelete]
        [Route("api/MoveAssignment/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var assignment = db.MoveAssignments.FirstOrDefault(a => a.Id == id);

                if (assignment == null)
                    return NotFound();

                // Vehicle ko wapis "Available" kar do agar koi aur active assignment nahi hai
                if (assignment.VehicleId.HasValue)
                {
                    var vehicle = db.Vehicles.FirstOrDefault(v => v.Id == assignment.VehicleId.Value);
                    if (vehicle != null)
                        vehicle.Status = "Available";
                }

                db.MoveAssignments.Remove(assignment);
                db.SaveChanges();

                return Ok(new { message = "Assignment removed successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class MoveAssignmentDto
    {
        public int MoveId { get; set; }
        public int? VehicleId { get; set; }
        public int? DriverId { get; set; }
        public int? CrewId { get; set; }
    }
}