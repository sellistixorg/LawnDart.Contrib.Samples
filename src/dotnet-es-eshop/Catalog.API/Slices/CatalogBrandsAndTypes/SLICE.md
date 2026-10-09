# Catalog Brands And Types

Status: Ready

Read model of brand and type names. Browse Catalog facets carry only ids and counts, so filter lists read names from here (upstream GET /api/catalog/catalogbrands and /catalogtypes).

`CatalogBrandsAndTypesProjection` folds `CatalogBrandDefined` and `CatalogTypeDefined` into `CatalogBrandsAndTypesView`.

GET `/views/catalog-brands-and-types`.

The catalog page reads brand and type names from this route. Brand and type ids are Guids. Browse Catalog supplies the rows and the counts. Brands and types are returned in definition order.
