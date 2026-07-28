using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.company
{
    [RoutePrefix("api/Branch")]
    public class BranchController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Branch?companyId=1
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAll(int companyId, int? parentBranchId = null)
        {
            try
            {
                var query = db.Branches.Where(b => b.CompanyId == companyId);

                if (parentBranchId.HasValue)
                    query = query.Where(b => b.ParentBranchId == parentBranchId.Value);

                var branches = query
                    .OrderBy(b => b.Name)
                    .Select(b => new
                    {
                        b.Id,
                        b.CompanyId,
                        b.ParentBranchId,
                        b.Name,
                        b.Address,
                        b.CreatedAt
                    })
                    .ToList();

                return Ok(branches);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Branch/5
        [HttpGet]
        [Route("{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var branch = db.Branches.FirstOrDefault(b => b.Id == id);

                if (branch == null)
                    return NotFound();

                return Ok(branch);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Branch/hierarchy?companyId=1
        [HttpGet]
        [Route("hierarchy")]
        public IHttpActionResult GetHierarchy(int companyId)
        {
            try
            {
                List<BranchTreeDto> allBranches = db.Branches
                    .Where(b => b.CompanyId == companyId)
                    .Select(b => new BranchTreeDto
                    {
                        Id = b.Id,
                        ParentBranchId = b.ParentBranchId,
                        Name = b.Name,
                        Address = b.Address
                    })
                    .ToList();

                var roots = allBranches
                    .Where(b => b.ParentBranchId == null)
                    .ToList();

                var result = roots
                    .Select(r => BuildTree(r.Id, allBranches))
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private object BuildTree(int branchId, List<BranchTreeDto> allBranches)
        {
            var current = allBranches.FirstOrDefault(b => b.Id == branchId);

            if (current == null)
                return null;

            var children = allBranches
                .Where(b => b.ParentBranchId == branchId)
                .ToList();

            return new
            {
                current.Id,
                current.Name,
                current.Address,
                Children = children
                    .Select(c => BuildTree(c.Id, allBranches))
                    .ToList()
            };
        }

        // POST api/Branch
        [HttpPost]
        [Route("")]
        public IHttpActionResult Create([FromBody] BranchDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                if (string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest("Branch Name is required.");

                var companyExists = db.Companies
                    .Any(c => c.Id == model.CompanyId && c.IsDeleted == false);

                if (!companyExists)
                    return BadRequest("Invalid CompanyId.");

                if (model.ParentBranchId.HasValue)
                {
                    var parentExists = db.Branches
                        .Any(b => b.Id == model.ParentBranchId.Value);

                    if (!parentExists)
                        return BadRequest("Invalid ParentBranchId.");
                }

                var branch = new Branch
                {
                    CompanyId = model.CompanyId,
                    ParentBranchId = model.ParentBranchId,
                    Name = model.Name,
                    Address = model.Address,
                    CreatedAt = DateTime.Now
                };

                db.Branches.Add(branch);
                db.SaveChanges();

                return Ok(new
                {
                    message = "Branch created successfully.",
                    id = branch.Id
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Branch/5
        [HttpPut]
        [Route("{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] BranchDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var branch = db.Branches.FirstOrDefault(b => b.Id == id);

                if (branch == null)
                    return NotFound();

                if (model.ParentBranchId.HasValue &&
                    model.ParentBranchId.Value == id)
                {
                    return BadRequest("A branch cannot be its own parent.");
                }

                branch.Name = model.Name;
                branch.Address = model.Address;
                branch.ParentBranchId = model.ParentBranchId;

                db.SaveChanges();

                return Ok(new
                {
                    message = "Branch updated successfully."
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Branch/5
        [HttpDelete]
        [Route("{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var branch = db.Branches.FirstOrDefault(b => b.Id == id);

                if (branch == null)
                    return NotFound();

                var hasChildren = db.Branches
                    .Any(b => b.ParentBranchId == id);

                if (hasChildren)
                {
                    return BadRequest(
                        "Cannot delete a branch that has sub-branches. Delete or reassign sub-branches first."
                    );
                }

                var hasUsers = db.Users
                    .Any(u => u.BranchId == id && u.IsDeleted == false);

                if (hasUsers)
                {
                    return BadRequest(
                        "Cannot delete a branch that has active users assigned."
                    );
                }

                db.Branches.Remove(branch);
                db.SaveChanges();

                return Ok(new
                {
                    message = "Branch deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class BranchDto
    {
        public int CompanyId { get; set; }
        public int? ParentBranchId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
    }

    public class BranchTreeDto
    {
        public int Id { get; set; }
        public int? ParentBranchId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
    }
}