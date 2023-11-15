## Table of contents

<!-- toc -->

- [Getting started](#getting-started)
- [Routes](#routes)
  * [Plural routes](#plural-routes)
  * [Singular routes](#singular-routes)
  * [Filter](#filter)
  * [Paginate](#paginate)
  * [Sort](#sort)
  * [Slice](#slice)
  * [Operators](#operators)
  * [Full-text search](#full-text-search)
  * [Relationships](#relationships)
  * [Database](#database)
  * [Homepage](#homepage)
- [Extras](#extras)
  * [Static file server](#static-file-server)
  * [Alternative port](#alternative-port)
  * [Access from anywhere](#access-from-anywhere)
  * [Remote schema](#remote-schema)
  * [Generate random data](#generate-random-data)
  * [HTTPS](#https)
  * [Add custom routes](#add-custom-routes)
  * [Add middlewares](#add-middlewares)
  * [CLI usage](#cli-usage)
  * [Module](#module)
    + [Simple example](#simple-example)
    + [Custom routes example](#custom-routes-example)
    + [Access control example](#access-control-example)
    + [Custom output example](#custom-output-example)
    + [Rewriter example](#rewriter-example)
    + [Mounting JSON Server on another endpoint example](#mounting-json-server-on-another-endpoint-example)
    + [API](#api)
  * [Deployment](#deployment)
- [Links](#links)
  * [Video](#video)
  * [Articles](#articles)
  * [Third-party tools](#third-party-tools)
- [License](#license)

<!-- tocstop -->

## Getting started
   
Install LAK.Sdk.Core packages

```
nuget install LAK.Sdk.Core -OutputDirectory packages
```

I have an entity model `Product` and `ProductFilter` model with fields:

```c
public class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; }
    
    public decimal Price { get; set; }

    public int Quantity { get; set; }
    
    public bool Status { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
```

```c
public class ProductFilter
{
    public Guid? Id { get; set; }

    public string? Name { get; set; }
    
    public decimal? Price { get; set; }

    public int? Quantity { get; set; }
    
    public bool? Status { get; set; }
    
    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
```

This is my `ProductService` to get products:

```bash
public async Task<Product> GetAllAsync()
{
   var products = await _context.Products.ToListAsync();

    return products;
}
```

## Filter

Use `DynamicFilter` from LAK.Sdk.Core to filter:

```bash
public async Task<Product> GetAllAsync(ProductFilter filter)
{
   var products = await _context.Products.AsQueryable().DynamicFilter(filter).ToListAsync();

    return products;
}
```

Now if you provide filter on query params url [http://localhost:3000/api/product?name=pepsi&quantity=2&status=true](http://localhost:3000/api/product?name=pepsi&quantity=2&status=true), you'll get

```c
[
    {
        "id": "6656d6c5-923c-48d0-bc75-19bab87671a3",
        "name": "Pepsi",
        "price": 2000,
        "quantity": 2,
        "status": 1,
        "createdAt": "2023-11-14T09:11:27.9",
        "updatedAt": null
    }
]
```

- Advance filter DateTime with property `DateOperators`:
  * `eq`: equal.
  * `gt`: greater than.
  * `gte`: greater than or equal.
  * `lt`: less than.
  * `lte`: less than or equal

Update `ProductFilter` model:

```c#
public class ProductFilter
{
    ...
    
    public string? DateOperators { get; set; }
}
```

```
DateOperators=gte
```

Now if you provide filter on query params url [http://localhost:3000/api/product?CreatedAt=2023-11-14T09%3A11&DateOperators=gte](http://localhost:3000/api/product?CreatedAt=2023-11-14T09%3A11&DateOperators=gte), you'll get

```c
[
    {
        "id": "6656d6c5-923c-48d0-bc75-19bab87671a3",
        "name": "Pepsi",
        "price": 2000,
        "quantity": 5,
        "status": 1,
        "createdAt": "2023-11-14T09:11:27.9",
        "updatedAt": null
    },
    {
        "id": "5490c318-5232-4e4e-9b0e-1defb4f424e6",
        "name": "Coca",
        "price": 2000,
        "quantity": 6,
        "status": 2,
        "createdAt": "2023-11-14T09:11:23.757",
        "updatedAt": "2023-11-14T09:11:23.757"
    }
]
```

- Use `,` inside `DateOperators` to filter many DateTime properties with many conditions: `eq`, `gt`, `gte`, `lt`, `lte`.

```
DateOperators=gte,lte
```

Now if you provide filter on query params url [http://localhost:3000/api/product?CreatedAt=2023-11-14T09%3A11&UpdatedAt=2023-11-14T09%3A15&DateOperators=gte%2Clte](http://localhost:3000/api/product?CreatedAt=2023-11-14T09%3A11&UpdatedAt=2023-11-14T09%3A15&DateOperators=gte%2Clte), you'll get

```c
[
    {
        "id": "5490c318-5232-4e4e-9b0e-1defb4f424e6",
        "name": "Coca",
        "price": 2000,
        "quantity": 6,
        "status": 2,
        "createdAt": "2023-11-14T09:11:23.757",
        "updatedAt": "2023-11-14T09:11:23.757"
    }
]
```

## Date Range Filter
## Sort
## Paging


