using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;

/// <summary>
/// Read model of brand and type names. Browse Catalog facets carry only ids and counts, so filter lists read names from here (upstream GET /api/catalog/catalogbrands and /catalogtypes).
/// </summary>
[GlobalProjection(Name)]
public sealed class CatalogBrandsAndTypesProjection : ProjectionBase<CatalogBrandsAndTypesView>
{
    /// <summary>
    /// Logical projection name. The view store key is this name plus <c>:v1</c>.
    /// </summary>
    public const string Name = "CatalogBrandsAndTypes";

    /// <summary>
    /// View store key for version 1 of <see cref="Name"/>.
    /// </summary>
    public const string StorageKey = Name + ":v1";

    /// <summary>
    /// Adds the brand id and brand display name. A later event for the same brand id replaces the name.
    /// </summary>
    /// <param name="defined">Catalog Brand Defined.</param>
    public void Handle(CatalogBrandDefined defined)
    {
        var name = defined.Name ?? "";
        var index = State.Brands.FindIndex(brand => brand.BrandId == defined.BrandId);
        if (index >= 0)
        {
            State.Brands[index].Name = name;
            return;
        }

        State.Brands.Add(new CatalogBrandName
        {
            BrandId = defined.BrandId,
            Name = name
        });
    }

    /// <summary>
    /// Adds the type id and type display name. A later event for the same type id replaces the name.
    /// </summary>
    /// <param name="defined">Catalog Type Defined.</param>
    public void Handle(CatalogTypeDefined defined)
    {
        var name = defined.Name ?? "";
        var index = State.Types.FindIndex(type => type.TypeId == defined.TypeId);
        if (index >= 0)
        {
            State.Types[index].Name = name;
            return;
        }

        State.Types.Add(new CatalogTypeName
        {
            TypeId = defined.TypeId,
            Name = name
        });
    }
}
