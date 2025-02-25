using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Elsa.Models;
using Elsa.Persistence;
using Elsa.Persistence.Specifications.WorkflowInstances;
using Elsa.Services.Models;
using Open.Linq.AsyncExtensions;

namespace Elsa.Services
{
    public class WorkflowRegistry : IWorkflowRegistry
    {
        private readonly IEnumerable<IWorkflowProvider> _workflowProviders;
        private readonly IWorkflowInstanceStore _workflowInstanceStore;

        public WorkflowRegistry(IEnumerable<IWorkflowProvider> workflowProviders, IWorkflowInstanceStore workflowInstanceStore)
        {
            _workflowProviders = workflowProviders;
            _workflowInstanceStore = workflowInstanceStore;
        }

        public async Task<IEnumerable<IWorkflowBlueprint>> ListAsync(CancellationToken cancellationToken) => await GetWorkflowsInternalAsync(cancellationToken).ToListAsync(cancellationToken);
        public async Task<IEnumerable<IWorkflowBlueprint>> ListActiveAsync(CancellationToken cancellationToken) => await ListActiveWorkflowsAsync(cancellationToken).ToListAsync(cancellationToken);

        public async Task<IWorkflowBlueprint?> GetAsync(string id, string? tenantId, VersionOptions version, CancellationToken cancellationToken) =>
            await FindAsync(x => x.Id == id && x.TenantId == tenantId && x.WithVersion(version), cancellationToken);

        public async Task<IEnumerable<IWorkflowBlueprint>> FindManyAsync(Func<IWorkflowBlueprint, bool> predicate, CancellationToken cancellationToken) =>
            (await ListAsync(cancellationToken).Where(predicate).OrderByDescending(x => x.Version)).ToList();

        public async Task<IWorkflowBlueprint?> FindAsync(Func<IWorkflowBlueprint, bool> predicate, CancellationToken cancellationToken) =>
            (await ListAsync(cancellationToken).Where(predicate).OrderByDescending(x => x.Version)).FirstOrDefault();
        
        private async IAsyncEnumerable<IWorkflowBlueprint> ListActiveWorkflowsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var workflows = await ListAsync(cancellationToken);

            foreach (var workflow in workflows)
            {
                // If a workflow is not published, only consider it for processing if it has at least one non-ended workflow instance.
                if (!workflow.IsPublished && !await WorkflowHasNonFinishedWorkflowsAsync(workflow, cancellationToken))
                    continue;

                yield return workflow;
            }
        }

        private async Task<bool> WorkflowHasNonFinishedWorkflowsAsync(IWorkflowBlueprint workflowBlueprint, CancellationToken cancellationToken)
        {
            var count = await _workflowInstanceStore.CountAsync(new NonFinalizedWorkflowSpecification().WithWorkflowDefinition(workflowBlueprint.Id), cancellationToken);
            return count > 0;
        }

        private async IAsyncEnumerable<IWorkflowBlueprint> GetWorkflowsInternalAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var providers = _workflowProviders;

            foreach (var provider in providers)
            await foreach (var workflow in provider.GetWorkflowsAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                yield return workflow;
            }
        }
    }
}