using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IMemberService
    {
        List<MemberViewModel> GetMembers(string search = null);
        MemberViewModel GetMember(int id);
        void AddMember(MemberViewModel model);
        void UpdateMember(MemberViewModel model);
        void ToggleStatus(int id);
        void CreateMemberFromAccountRequest(string studentNumber, string fullName, string courseYear, string section);
    }
}
