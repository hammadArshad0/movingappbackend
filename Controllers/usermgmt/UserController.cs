using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;

namespace task_full_stack.Controllers.usermgmt
{
    // ================= USERS =================
    public class UserController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/User?companyId=1&branchId=2
        [HttpGet]
        [Route("api/User")]
        public IHttpActionResult GetAll(int companyId, int? branchId = null)
        {
            try
            {
                var query = db.Users.Where(u => u.CompanyId == companyId && u.IsDeleted == false);

                if (branchId.HasValue)
                    query = query.Where(u => u.BranchId == branchId.Value);

                var users = query
                             .Select(u => new
                             {
                                 u.Id,
                                 u.FirstName,
                                 u.LastName,
                                 u.Email,
                                 u.BranchId,
                                 u.RoleId,
                                 RoleName = db.Roles.Where(r => r.Id == u.RoleId).Select(r => r.Name).FirstOrDefault(),
                                 u.CreatedAt
                             })
                             .ToList();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/User/5
        [HttpGet]
        [Route("api/User/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.Id == id && u.IsDeleted == false);

                if (user == null)
                    return NotFound();

                return Ok(new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.CompanyId,
                    user.BranchId,
                    user.RoleId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/User/5/menu
        // Permission-based dynamic menu - Role.Permissions navigation property use karte hain
        // (koi alag RolePermissions table/entity nahi hai, EF ne many-to-many collapse kar diya hai)
        [HttpGet]
        [Route("api/User/{id:int}/menu")]
        public IHttpActionResult GetUserMenu(int id)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.Id == id && u.IsDeleted == false);
                if (user == null)
                    return NotFound();

                if (!user.RoleId.HasValue)
                    return Ok(new { userId = id, permissions = new string[0] });

                var permissions = db.Roles
                                     .Where(r => r.Id == user.RoleId.Value)
                                     .SelectMany(r => r.Permissions)
                                     .Select(p => p.Name)
                                     .ToList();

                return Ok(new { userId = id, permissions });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/User
        [HttpPost]
        [Route("api/User")]
        public IHttpActionResult Create([FromBody] CreateUserDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
                    return BadRequest("Email and Password are required.");

                var duplicateEmail = db.Users.Any(u => u.Email == model.Email);
                if (duplicateEmail)
                    return BadRequest("A user with this email already exists.");

                var roleExists = db.Roles.Any(r => r.Id == model.RoleId);
                if (!roleExists)
                    return BadRequest("Invalid RoleId.");

                var user = new User
                {
                    CompanyId = model.CompanyId,
                    BranchId = model.BranchId,
                    RoleId = model.RoleId,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PasswordHash = HashPassword(model.Password),
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                db.Users.Add(user);
                db.SaveChanges();

                return Ok(new { message = "User created successfully.", id = user.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/User/5
        [HttpPut]
        [Route("api/User/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] UpdateUserDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var user = db.Users.FirstOrDefault(u => u.Id == id && u.IsDeleted == false);

                if (user == null)
                    return NotFound();

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.BranchId = model.BranchId;
                user.RoleId = model.RoleId;

                db.SaveChanges();

                return Ok(new { message = "User updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/User/5/change-password
        [HttpPut]
        [Route("api/User/{id:int}/change-password")]
        public IHttpActionResult ChangePassword(int id, [FromBody] ChangePasswordDto model)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.Id == id && u.IsDeleted == false);

                if (user == null)
                    return NotFound();

                if (user.PasswordHash != HashPassword(model.OldPassword))
                    return BadRequest("Old password is incorrect.");

                if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
                    return BadRequest("New password must be at least 6 characters.");

                user.PasswordHash = HashPassword(model.NewPassword);
                db.SaveChanges();

                return Ok(new { message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/User/5
        // Soft delete
        [HttpDelete]
        [Route("api/User/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.Id == id && u.IsDeleted == false);

                if (user == null)
                    return NotFound();

                user.IsDeleted = true;
                db.SaveChanges();

                return Ok(new { message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }


    // ================= ROLES =================
    public class RoleController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        [HttpGet]
        [Route("api/Role")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var roles = db.Roles.Select(r => new { r.Id, r.Name }).ToList();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Role/5/permissions
        // RolePermissions table exist nahi karti - Role.Permissions navigation property (many-to-many) use hoti hai
        [HttpGet]
        [Route("api/Role/{id:int}/permissions")]
        public IHttpActionResult GetPermissions(int id)
        {
            try
            {
                var roleExists = db.Roles.Any(r => r.Id == id);
                if (!roleExists)
                    return NotFound();

                var permissions = db.Roles
                                     .Where(r => r.Id == id)
                                     .SelectMany(r => r.Permissions)
                                     .Select(p => new { p.Id, p.Name })
                                     .ToList();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("api/Role")]
        public IHttpActionResult Create([FromBody] RoleDto model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest("Role Name is required.");

                var duplicate = db.Roles.Any(r => r.Name == model.Name);
                if (duplicate)
                    return BadRequest("Role already exists.");

                var role = new Role { Name = model.Name };
                db.Roles.Add(role);
                db.SaveChanges();

                return Ok(new { message = "Role created successfully.", id = role.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Role/5/assign-permission
        // Navigation collection mein directly Permission entity add karte hain (junction row EF khud manage karta hai)
        [HttpPost]
        [Route("api/Role/{id:int}/assign-permission")]
        public IHttpActionResult AssignPermission(int id, [FromBody] AssignPermissionDto model)
        {
            try
            {
                var role = db.Roles.FirstOrDefault(r => r.Id == id);
                if (role == null)
                    return NotFound();

                var permission = db.Permissions.FirstOrDefault(p => p.Id == model.PermissionId);
                if (permission == null)
                    return BadRequest("Invalid PermissionId.");

                var alreadyAssigned = role.Permissions.Any(p => p.Id == model.PermissionId);
                if (alreadyAssigned)
                    return BadRequest("Permission already assigned to this role.");

                role.Permissions.Add(permission);
                db.SaveChanges();

                return Ok(new { message = "Permission assigned successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Role/5/permission/10
        [HttpDelete]
        [Route("api/Role/{id:int}/permission/{permissionId:int}")]
        public IHttpActionResult RemovePermission(int id, int permissionId)
        {
            try
            {
                var role = db.Roles.FirstOrDefault(r => r.Id == id);
                if (role == null)
                    return NotFound();

                var permission = role.Permissions.FirstOrDefault(p => p.Id == permissionId);
                if (permission == null)
                    return NotFound();

                role.Permissions.Remove(permission);
                db.SaveChanges();

                return Ok(new { message = "Permission removed from role successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }


    // ================= PERMISSIONS (lookup) =================
    public class PermissionController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        [HttpGet]
        [Route("api/Permission")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var permissions = db.Permissions.Select(p => new { p.Id, p.Name }).ToList();
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("api/Permission")]
        public IHttpActionResult Create([FromBody] PermissionDto model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest("Permission Name is required.");

                var permission = new Permission { Name = model.Name };
                db.Permissions.Add(permission);
                db.SaveChanges();

                return Ok(new { message = "Permission created successfully.", id = permission.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class CreateUserDto
    {
        public int CompanyId { get; set; }
        public int? BranchId { get; set; }
        public int RoleId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class UpdateUserDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int? BranchId { get; set; }
        public int? RoleId { get; set; }
    }

    public class ChangePasswordDto
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class RoleDto
    {
        public string Name { get; set; }
    }

    public class PermissionDto
    {
        public string Name { get; set; }
    }

    public class AssignPermissionDto
    {
        public int PermissionId { get; set; }
    }
}