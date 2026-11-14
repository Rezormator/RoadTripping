using UnityEngine;
using Zenject;

public class CarInstaller : MonoInstaller
{
    [SerializeField] private CarConfig config;

    public override void InstallBindings()
    {
        Container.BindInstance(config);
        Container.Bind<ICarInput>().To<CarInputReader>().AsSingle();
        Container.Bind<CarDrivingService>().AsSingle();
    }
}
