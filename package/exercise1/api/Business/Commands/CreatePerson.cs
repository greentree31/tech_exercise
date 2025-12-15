using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using System.Net;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Commands
{
    public class CreatePerson : IRequest<CreatePersonResult>
    {
        public required string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
    }

    public class CreatePersonPreProcessor : IRequestPreProcessor<CreatePerson>
    {
        private readonly StargateContext _context;
        public CreatePersonPreProcessor(StargateContext context)
        {
            _context = context;
        }
        public Task Process(CreatePerson request, CancellationToken cancellationToken)
        {
            var person = _context.People.AsNoTracking().FirstOrDefault(z => z.LastName == request.LastName);

            if (person is not null) throw new BadHttpRequestException("Bad Request");

            return Task.CompletedTask;
        }
    }

    public class CreatePersonHandler : IRequestHandler<CreatePerson, CreatePersonResult>
    {
        private readonly StargateContext _context;

        public CreatePersonHandler(StargateContext context)
        {
            _context = context;
        }
        public async Task<CreatePersonResult> Handle(CreatePerson request, CancellationToken cancellationToken)
        {
            var existing = await _context.People.AsNoTracking().FirstOrDefaultAsync(p => p.LastName == request.LastName, cancellationToken);
            
            if (existing is not null)
            {
                return new CreatePersonResult
                {
                    Id = existing.Id,
                    Success = false,
                    Message = "Person already exists.",
                    ResponseCode = (int)HttpStatusCode.BadRequest
                };

            }
            var newPerson = new Person()
                {
                   LastName = request.LastName
                };

                await _context.People.AddAsync(newPerson);

                await _context.SaveChangesAsync();

                return new CreatePersonResult()
                {
                    Id = newPerson.Id
                };
          
        }
    }

    public class CreatePersonResult : BaseResponse
    {
        public int Id { get; set; }
    }
}
