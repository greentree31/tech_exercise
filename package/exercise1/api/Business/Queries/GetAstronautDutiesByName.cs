using Dapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;
using StargateAPI.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StargateAPI.Business.Queries
{
    public class GetAstronautDutiesByName : IRequest<GetAstronautDutiesByNameResult>
    {
        public string Name { get; set; } = string.Empty;
    }

    public class GetAstronautDutiesByNameHandler
        : IRequestHandler<GetAstronautDutiesByName, GetAstronautDutiesByNameResult>
    {
        private readonly StargateContext _context;

        public GetAstronautDutiesByNameHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<GetAstronautDutiesByNameResult> Handle(
            GetAstronautDutiesByName request,
            CancellationToken cancellationToken)
        {
            var result = new GetAstronautDutiesByNameResult();

            // Get the person + astronaut summary using Dapper
            var query = @"
        SELECT  
        a.Id AS PersonId,
        WHERE a.FirstName = @FirstName AND a.LastName = @LastName
        b.CurrentRank,
        b.CurrentDutyTitle,
        b.CareerStartDate,
        b.CareerEndDate FROM [Person] 
        a LEFT JOIN [AstronautDetail] 
        b ON 
        b.PersonId = a.Id WHERE a.Name = @Name";

            var person = await _context.Connection
                .QueryFirstOrDefaultAsync<PersonAstronautDto>(query, new { Name = request.Name });

            if (person is null)
            {
                result.Success = false;
                result.Message = "Person not found.";
                result.ResponseCode = 404;
                return result;
            }

            result.Person = person;

            // Load all duties for this person using EF Core
            var duties = await _context.AstronautDuties
                .Where(d => d.PersonId == person.PersonId)
                .OrderBy(d => d.DutyStartDate)
                .ToListAsync(cancellationToken);

            result.Duties = duties
                .Select(d => new AstronautDutyDto
                {
                    Rank = d.Rank,
                    DutyTitle = d.DutyTitle,
                    DutyStartDate = d.DutyStartDate,
                    DutyEndDate = d.DutyEndDate
                })
                .ToList();

            result.Success = true;
            result.Message = "Successful";
            result.ResponseCode = 200;

            return result;
        }
    }

    public class AstronautDutyDto
    {
        public string Rank { get; set; } = string.Empty;
        public string DutyTitle { get; set; } = string.Empty;
        public DateTime DutyStartDate { get; set; }
        public DateTime? DutyEndDate { get; set; }
    }

    public class GetAstronautDutiesByNameResult : BaseResponse
    {
        public PersonAstronautDto? Person { get; set; }

        // NEW: full duty history
        public List<AstronautDutyDto> Duties { get; set; } = new();
    }
}
