﻿using System;
using System.Linq;
using System.Threading.Tasks;
using AllReady.Features.Notifications;
using AllReady.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AllReady.Areas.Admin.Features.Tasks
{
    public class ChangeTaskStatusCommandHandler : IAsyncRequestHandler<ChangeTaskStatusCommand, TaskChangeResult>
    {
        public Func<DateTime> DateTimeUtcNow = () => DateTime.UtcNow;

        private readonly AllReadyContext _context;
        private readonly IMediator _mediator;

        public ChangeTaskStatusCommandHandler(AllReadyContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<TaskChangeResult> Handle(ChangeTaskStatusCommand message)
        {
            var @task = await GetTask(message);
            if (@task == null)
            {
                throw new InvalidOperationException($"Task {message.TaskId} does not exist");
            }
            
            var taskSignup = @task.AssignedVolunteers.SingleOrDefault(c => c.User.Id == message.UserId);
            if (taskSignup == null)
            {
                throw new InvalidOperationException($"Sign-up for user {message.UserId} does not exist");
            }

            switch (message.TaskStatus)
            {
                case AllReady.Models.TaskStatus.Assigned:
                    break;
                case AllReady.Models.TaskStatus.Accepted:
                    if (taskSignup.Status != AllReady.Models.TaskStatus.Assigned && taskSignup.Status != AllReady.Models.TaskStatus.CanNotComplete && taskSignup.Status != AllReady.Models.TaskStatus.Completed) 
                        throw new ArgumentException("Task must be assigned before being accepted or undoing CanNotComplete or Completed");
                    break;
                case AllReady.Models.TaskStatus.Rejected:
                    if (taskSignup.Status != AllReady.Models.TaskStatus.Assigned)
                        throw new ArgumentException("Task must be assigned before being rejected");
                    break;
                case AllReady.Models.TaskStatus.Completed:
                    if (taskSignup.Status != AllReady.Models.TaskStatus.Accepted && taskSignup.Status != AllReady.Models.TaskStatus.Assigned)
                        throw new ArgumentException("Task must be accepted before being completed");
                    break;
                case AllReady.Models.TaskStatus.CanNotComplete:
                    if (taskSignup.Status != AllReady.Models.TaskStatus.Accepted && taskSignup.Status != AllReady.Models.TaskStatus.Assigned)
                        throw new ArgumentException($"Task must be assigned or accepted before it can be marked as {message.TaskStatus}");
                    break;
                default:
                    throw new ArgumentException($"Invalid sign-up status value: {message.TaskStatus}");
            }

            taskSignup.Status = message.TaskStatus;
            taskSignup.StatusDateTimeUtc = DateTimeUtcNow();
            taskSignup.StatusDescription = message.TaskStatusDescription;

            await _context.SaveChangesAsync();

            var notification = new TaskSignupStatusChanged { SignupId = taskSignup.Id };
            await _mediator.PublishAsync(notification);
            
            return new TaskChangeResult { Status = "success", Task = @task };
        }

        private async Task<AllReadyTask> GetTask(ChangeTaskStatusCommand message)
        {
            return await _context.Tasks
                .Include(t => t.AssignedVolunteers).ThenInclude(ts => ts.User)
                .Include(t => t.RequiredSkills).ThenInclude(s => s.Skill)
                .SingleOrDefaultAsync(c => c.Id == message.TaskId);
        }
    }

    public class TaskChangeResult
    {
        public string Status { get; set; }
        public AllReadyTask Task { get; set; }
    }
}