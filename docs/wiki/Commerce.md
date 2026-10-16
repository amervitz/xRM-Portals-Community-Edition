# Commerce

The portal includes a shopping cart, checkout and payment-provider integration. It is built on Dataverse
opportunity, quote, order and invoice records, with portal records for the cart.

> **Review before enabling on a public site.** This is a legacy commerce stack with placeholder
> credentials in the sample configuration. Verify the payment provider, tax and fulfilment behaviour
> against your requirements, and never commit real payment credentials.

## Records

| Record | Role |
|---|---|
| `adx_shoppingcart` | A visitor's cart. |
| `adx_shoppingcartitem` | A line in the cart. |
| `adx_product` | A catalog product. |
| `adx_productpricelevel` | Price list / price level. |
| `adx_discount` | A discount definition. |
| `opportunity`, `quote`, `salesorder`, `invoice` | The Dataverse sales pipeline the portal creates and updates. |
| `adx_webpage` purchase columns | `adx_purchase*` columns configure what a storefront page buys. |

The purchase configuration on a web page (`adx_purchase*`) maps the page to a product and controls how
line items are produced, including the relationships used to find the product, quantity and units
(`adx_purchaselineitem*`), whether shipping is required (`adx_purchaserequiresshipping`), and the
target entity relationships for the order and invoice (`adx_purchasetargetentity*`).

## Settings

| Setting | Effect |
|---|---|
| `Ecommerce/PaymentProvider` | The active payment provider. Defaults to **`PayPal`** when unset. |
| `Ecommerce/Paypal/AccountEmail` | The PayPal account email. |
| `Ecommerce/Paypal/PaypalBaseUrl` | PayPal endpoint (sandbox vs. live). |
| `Ecommerce/PayPal/PDTIdentityToken` | PayPal Payment Data Transfer identity token. |
| `Ecommerce/CreateInvoiceOnVerification` | Whether an invoice is created when payment is verified. |
| `Ecommerce/DefaultPriceLevelName` | The default price list. |
| `ProductFiltering/Enabled` | Enables product filtering by account/contact relationships. |
| `ProductFiltering/AccountToProductRelationshipNames` | Relationship names used to filter products for an account. |
| `ProductFiltering/ContactToProductRelationshipNames` | The same for a contact. |
| `ProductFiltering/DisplayArticlesWithoutAssociatedProducts` | Whether products with no association are shown. |

Legacy Authorize.Net credentials can also be supplied as app settings (`ApiLogin`, `TransactionKey`,
`MerchantHash`). **Do not put real values in source-controlled configuration.**

## Payment providers

`PaymentProcessingRequestHandlerFactory` is mapped to the `PaymentProcessing.axd` handler in
`Web.config`. It resolves the provider from `Ecommerce/PaymentProvider` and dispatches to one of the
built-in handlers:

| Handler | Purpose |
|---|---|
| `PayPalPaymentHandler` | PayPal checkout. |
| `PayPalPaymentDataTransferHandler` | PayPal Payment Data Transfer (PDT) verification callback. |
| `AuthorizeNetPaymentHandler` | Authorize.Net checkout. |
| `AuthorizeNetPaymentHandlerBase` | Shared Authorize.Net plumbing. |
| `DemoPaymentHandler` | A no-op handler for demonstrations and testing. |
| `DemoPaypalRequestHandler` | A simulated PayPal request handler. |
| `PaymentHandler` | The abstract base every handler implements (`IHttpHandler`). |

The flow is: the storefront creates a cart, checkout hands off to the provider, the provider returns to
`PaymentProcessing.axd`, the handler verifies the payment, and the corresponding Dataverse records are
created or updated (optionally creating an invoice, per `Ecommerce/CreateInvoiceOnVerification`).

## Code layout

`Framework/Adxstudio.Xrm/Commerce` contains:

| Group | Types |
|---|---|
| Cart and purchase | `ShoppingCart`, `ShoppingCartDataAdapter`, `ShoppingCartItem`, `IShoppingCart`, `IShoppingCartItem`, `IShoppingCartDataAdapter`, `PurchaseDataAdapter`, `IPurchaseDataAdapter`, `QuotePurchaseDataAdapter`, `WebFormPurchaseDataAdapter` |
| Purchasables | `Purchasable`, `PurchasableItemOptions`, `IPurchasable`, `IPurchasableItem`, `IPurchasableItemOptions`, `PurchaseableItem` |
| Orders and quotes | `CommerceOrder`, `CommerceQuote`, `QuoteFunctions` |
| Pricing | `IPriceListProvider`, `LineItem`, `Discount`, `DiscountCodeValidationResult`, `IDiscount` |
| Payment | The handlers listed above, plus `PaymentRouteHandler` and `PaymentProcessingRequestHandlerFactory` |
| Dependencies | `IDataAdapterDependencies`, `PortalContextDataAdapterDependencies`, `PortalConfigurationDataAdapterDependencies`, `DataAdapterDependencies` |

Portal UI lives in `Samples/MasterPortal/Areas/Commerce` (`ShoppingCartController`), with
`Areas/Products` (`ProductsController`, `ReviewController`) for catalog and review experiences.

## Product filtering

When `ProductFiltering/Enabled` is on, the catalog is filtered to products related to the signed-in user
or their account through the configured relationships. This is how a partner or B2B catalog shows only
what a customer is entitled to buy.

**It is a catalog filter, not a security boundary.** The product and price records still need table
permissions.

## Reviews

Product reviews are Dataverse `adx_review` records with metadata:
`adx_reviewercontact`, `adx_revieweremail`, `adx_reviewername`, `adx_reviewerlocation`,
`adx_reviewercontact`. The Reviews UI is in the Products area.

## Packages repository

The portal also includes a package repository feature (`adx_package`, `adx_packageversion`,
`adx_packagecomponent`, `adx_packagedependency`, `adx_packagepublisher`, `adx_packagecategory`,
`adx_packageimage`) surfaced by `Areas/EntityList/Controllers/PackageRepositoryController.cs` and
`EntityListPackageRepositoryDataAdapter`. Despite the "commerce-adjacent" shape, this is used for
distributing portal packages, not for selling products.

## Enabling commerce

1. Decide the payment provider and configure it in a **non-committed** configuration source.
2. Set `Ecommerce/PaymentProvider` and `Ecommerce/DefaultPriceLevelName`.
3. Populate products and price levels, and set the `adx_purchase*` columns on the relevant pages.
4. Configure table permissions for cart, product, discount and the sales records.
5. Test the complete flow end to end against the provider's **sandbox**: add to cart → checkout → provider
   return → `PaymentProcessing.axd` → record creation → confirmation.
6. Only then point at the live provider.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Checkout returns to a blank page | Payment provider misconfigured, or `PaymentProcessing.axd` handler not registered/reachable. |
| Cart does not persist | Cart records not created — check table permissions and that the cart adapter is configured. |
| Prices are zero | `Ecommerce/DefaultPriceLevelName` does not match a populated price level. |
| Discounts are not applied | Discount records or `IDiscount` validation (validity dates, codes, minimum purchase). |
| Order created but no invoice | `Ecommerce/CreateInvoiceOnVerification` is off, or the invoice relationship is not configured. |
| Partner sees the wrong catalog | `ProductFiltering/*` relationship names do not match your schema. |

## Related pages

- [Events and Conferences](Events-and-Conferences.md) — paid registrations
- [Security and Authentication](Security-and-Authentication.md)
- [Site Settings Reference](Site-Settings-Reference.md#commerce)
- [Configuration Reference](Configuration-Reference.md) — handler registration
