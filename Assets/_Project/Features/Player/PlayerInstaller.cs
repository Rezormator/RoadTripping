using UnityEngine;
using Zenject;

public class PlayerInstaller : MonoInstaller
{
    [SerializeField] private PlayerMovementConfig movementConfig;
    [SerializeField] private CameraBobConfig cameraBobConfig;

    public override void InstallBindings()
    {
        Container.BindInstance(movementConfig);
        Container.BindInstance(cameraBobConfig);

        Container.Bind<IPlayerInput>().To<PlayerInputReader>().AsSingle();

        Container.Bind<PlayerLookService>().AsSingle();
        Container.Bind<PlayerCrouchService>().AsSingle();
        Container.Bind<PlayerMovementService>().AsSingle();
        Container.Bind<CameraBobService>().AsSingle();
        Container.Bind<PlayerService>().AsSingle();
    }
}
