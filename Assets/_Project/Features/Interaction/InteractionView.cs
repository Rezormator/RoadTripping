using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class InteractionView : MonoBehaviour
{
    private static readonly IComparer<RaycastHit> ByDistance =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    [SerializeField] private Transform viewPoint;

    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private InteractionService _interaction;
    private InteractionConfig _config;

    [Inject]
    public void Construct(InteractionService interaction, InteractionConfig config)
    {
        _interaction = interaction;
        _config = config;
    }

    private void Update()
    {
        if (_interaction == null)
            return;

        _interaction.Tick(FindTarget());
    }

    private IInteractable FindTarget()
    {
        int count = Physics.RaycastNonAlloc(viewPoint.position, viewPoint.forward, _hits,
                                            _config.Distance, _config.Mask, QueryTriggerInteraction.Ignore);
        Array.Sort(_hits, 0, count, ByDistance);

        Transform blockerRoot = null;
        float blockerDistance = 0f;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            if (hit.transform.IsChildOf(transform))
                continue;

            if (blockerRoot != null &&
                (hit.distance - blockerDistance > _config.SurfaceTolerance || hit.transform.root != blockerRoot))
                return null;

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
                return interactable;

            if (blockerRoot == null)
            {
                blockerRoot = hit.transform.root;
                blockerDistance = hit.distance;
            }
        }

        return null;
    }
}
