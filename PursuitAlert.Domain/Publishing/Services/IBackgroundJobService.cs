using PursuitAlert.Domain.Publishing.Models;
using System.Collections.Generic;

namespace PursuitAlert.Domain.Publishing.Services
{
    public interface IBackgroundJobService
    {
        #region Properties

        List<BackgroundJob> RunningJobs { get; }

        #endregion Properties
    }
}