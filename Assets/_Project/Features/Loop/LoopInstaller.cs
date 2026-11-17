using System.Linq;
using UnityEngine;
using Zenject;

public class LoopInstaller : MonoInstaller
{
    [SerializeField] private LoopConfig config;

    public override void InstallBindings()
    {
        Container.BindInstance(config);
        Container.Bind<LoopService>().AsSingle();
        Container.Bind<ILoopTraveler>()
            .FromMethodMultiple(_ => FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude).OfType<ILoopTraveler>())
            .AsCached();
    }
}
