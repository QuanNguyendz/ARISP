using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARISP.Domain.Constants
{
    public static class AppRoles
    {
        // Người trong công ty (trong class Users)
        public const string SuperAdmin = "Super_admin";
        public const string HrAdmin = "Hr_admin";
        public const string Recruiter = "Recruiter";

        // Người bên ngoài công ty (trong class Candidates)
        public const string Candidate = "Candidate";
    }
}
