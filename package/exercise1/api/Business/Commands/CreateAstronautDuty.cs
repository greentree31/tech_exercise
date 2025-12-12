using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using System.Net;

namespace StargateAPI.Business.Commands
{
    public class CreateAstronautDuty : IRequest<CreateAstronautDutyResult>
    {
        public required string Name { get; set; }
        public required string Rank { get; set; }
        public required string DutyTitle { get; set; }
        public DateTime DutyStartDate { get; set; }
    }

    // ---------------- PRE-PROCESSOR ----------------
    public class CreateAstronautDutyPreProcessor
        : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public async Task Process(
            CreateAstronautDuty request,
            CancellationToken cancellationToken)
        {
            var person = await _context.People
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == request.Name, cancellationToken);

            if (person == null)
                throw new BadHttpRequestException("Person does not exist");

            // Prevent duplicate duty start dates
            var duplicateDuty = await _context.AstronautDuties
                .AnyAsync(d =>
                    d.PersonId == person.Id &&
                    d.DutyStartDate == request.DutyStartDate.Date,
                    cancellationToken);

            if (duplicateDuty)
                throw new BadHttpRequestException("Astronaut duty already exists");
        }
    }

    // ---------------- HANDLER ----------------
    public class CreateAstronautDutyHandler
        : IRequestHandler<CreateAstronautDuty, CreateAstronautDutyResult>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<CreateAstronautDutyResult> Handle(
            CreateAstronautDuty request,
            CancellationToken cancellationToken)
        {
            var person = await _context.People
                .FirstAsync(p => p.Name == request.Name, cancellationToken);

            // Get current astronaut detail (if any)
            var detail = await _context.AstronautDetails
                .FirstOrDefaultAsync(d => d.PersonId == person.Id, cancellationToken);

            // Get latest duty
            var currentDuty = await _context.AstronautDuties
                .Where(d => d.PersonId == person.Id && d.DutyEndDate == null)
                .OrderByDescending(d => d.DutyStartDate)
                .FirstOrDefaultAsync(cancellationToken);

            // Close previous duty
            if (currentDuty != null)
            {
                currentDuty.DutyEndDate = request.DutyStartDate.AddDays(-1).Date;
            }

            // Create new duty
            var newDuty = new AstronautDuty
            {
                PersonId = person.Id,
                Rank = request.Rank,
                DutyTitle = request.DutyTitle,
                DutyStartDate = request.DutyStartDate.Date,
                DutyEndDate = null
            };

            await _context.AstronautDuties.AddAsync(newDuty, cancellationToken);

            // Create or update astronaut detail
            if (detail == null)
            {
                detail = new AstronautDetail
                {
                    PersonId = person.Id,
                    CareerStartDate = request.DutyStartDate.Date
                };
                await _context.AstronautDetails.AddAsync(detail, cancellationToken);
            }

            detail.CurrentRank = request.Rank;
            detail.CurrentDutyTitle = request.DutyTitle;

            // RETIRED logic
            if (request.DutyTitle == "RETIRED")
            {
                detail.CareerEndDate = request.DutyStartDate.AddDays(-1).Date;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new CreateAstronautDutyResult
            {
                Id = newDuty.Id,
                Success = true,
                Message = "Astronaut duty created successfully",
                ResponseCode = (int)HttpStatusCode.OK
            };
        }
    }

    // ---------------- RESULT ----------------
    public class CreateAstronautDutyResult : BaseResponse
    {
        public int? Id { get; set; }
    }
}
