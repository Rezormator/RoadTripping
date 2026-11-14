using UnityEngine;
using Zenject;

public class InteractionInstaller : MonoInstaller
{
    [SerializeField] private InteractionConfig config;

    public override void InstallBindings()
    {
        Container.BindInstance(config);
        Container.Bind<InteractionService>().AsSingle();
    }
}
