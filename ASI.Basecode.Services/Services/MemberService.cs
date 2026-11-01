using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class MemberService : IMemberService
    {
        private readonly IMemberRepository _repository;
        private readonly IMapper _mapper;

        public MemberService(IMemberRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public List<MemberViewModel> GetMembers(string search = null)
        {
            var query = _repository.GetMembers();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.FullName.Contains(search) ||
                                         m.StudentNumber.Contains(search));
            }
            return _mapper.Map<List<MemberViewModel>>(query.ToList());
        }

        public MemberViewModel GetMember(int id)
        {
            var member = _repository.GetMember(id);
            return member == null ? null : _mapper.Map<MemberViewModel>(member);
        }

        public void AddMember(MemberViewModel model)
        {
            if (_repository.StudentNumberExists(model.StudentNumber))
            {
                throw new InvalidDataException("Student number already exists.");
            }

            var member = new Member();
            _mapper.Map(model, member);
            if (string.IsNullOrWhiteSpace(member.Status))
            {
                member.Status = "active";
            }

            _repository.AddMember(member);
        }

        public void UpdateMember(MemberViewModel model)
        {
            var member = _repository.GetMember(model.MemberId);
            if (member == null)
            {
                throw new InvalidDataException("Member not found.");
            }

            if (_repository.StudentNumberExists(model.StudentNumber, model.MemberId))
            {
                throw new InvalidDataException("Student number already exists.");
            }

            _mapper.Map(model, member);
            _repository.UpdateMember(member);
        }

        public void ToggleStatus(int id)
        {
            var member = _repository.GetMember(id);
            if (member == null)
            {
                return;
            }

            member.Status = (member.Status ?? "active") == "active" ? "inactive" : "active";
            _repository.UpdateMember(member);
        }

        public void CreateMemberFromAccountRequest(string studentNumber, string fullName, string courseYear, string section)
        {
            if (_repository.StudentNumberExists(studentNumber))
            {
                return;
            }

            var member = new Member
            {
                StudentNumber = studentNumber,
                FullName = fullName,
                CourseYear = courseYear,
                Section = section,
                Status = "active"
            };

            _repository.AddMember(member);
        }
    }
}
