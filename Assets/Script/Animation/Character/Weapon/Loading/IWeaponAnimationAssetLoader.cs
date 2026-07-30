namespace CGame.Animation
{
    public interface IWeaponAnimationAssetLoader
    {
        IWeaponAnimationCatalogLoadOperation BeginLoadCatalog(
            string location);

        IWeaponAnimationDefinitionLoadOperation BeginLoadDefinition(
            string location);
    }
}
