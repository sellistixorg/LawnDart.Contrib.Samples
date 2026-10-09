# Third-party notices

## dotnet/eShop

Presentation files in this project are copied from [dotnet/eShop](https://github.com/dotnet/eShop) commit `dc7ea499cd356924fb6689b3702964a5869dbae9`.

Source paths at that commit:

- `src/WebApp/Components/App.razor`
- `src/WebApp/Components/Routes.razor`
- `src/WebApp/Components/Layout/`
- `src/WebApp/Components/Pages/Cart/`
- `src/WebApp/Components/Pages/Catalog/`
- `src/WebApp/Components/Pages/Checkout/`
- `src/WebApp/Components/Pages/Item/`
- `src/WebApp/Components/Pages/User/Orders.razor`
- `src/WebApp/Components/Pages/User/Orders.razor.css`
- `src/WebApp/Components/Chatbot/Chatbot.razor` (unused)
- `src/WebApp/Components/Chatbot/Chatbot.razor.css` (unused)
- `src/WebApp/Components/Chatbot/Chatbot.razor.js` (unused)
- `src/WebApp/Components/Chatbot/ShowChatbotButton.razor` (unused)
- `src/WebApp/Components/Chatbot/ShowChatbotButton.razor.css` (unused)
- `src/WebApp/Components/Chatbot/MessageProcessor.cs` (unused)
- `src/WebApp/wwwroot/css/app.css`
- `src/WebApp/wwwroot/css/normalize.css`
- `src/WebApp/wwwroot/fonts/`
- `src/WebApp/wwwroot/icons/`
- `src/WebApp/wwwroot/images/favicon.png`
- `src/WebApp/wwwroot/images/header-home.webp`
- `src/WebApp/wwwroot/images/header.webp`
- `src/WebApp/wwwroot/images/logo-footer.svg`
- `src/WebApp/wwwroot/images/logo-header.svg`
- `src/WebApp/wwwroot/js/` (`chat-dock.js`, `chat-eyes.js`, and `chat-fab.js` are unused)
- `src/WebApp/wwwroot/chat.png` (unused)
- `src/WebApp/wwwroot/chatbot.png` (unused)
- `src/WebAppComponents/Catalog/CatalogListItem.razor`
- `src/WebAppComponents/Catalog/CatalogListItem.razor.css`
- `src/WebAppComponents/Catalog/CatalogSearch.razor`
- `src/WebAppComponents/Catalog/CatalogSearch.razor.css`
- `src/WebAppComponents/Catalog/CatalogItem.cs`
- `src/WebAppComponents/Catalog/CatalogFacets.cs`
- `src/WebAppComponents/Item/ItemHelper.cs`
- `src/WebAppComponents/Services/PriceFormatter.cs`
- `src/WebAppComponents/Services/IProductImageUrlProvider.cs`

Those files now live under `Components/`, `wwwroot/`, `Models/`, and `Services/` in this project. Catalog, search, and item pages call the catalog client. The cart and the header bag call the basket client. Checkout posts Place Order. Orders read OrdersByShopper. Catalog, basket, and ordering service implementations, the gRPC basket client, OpenID Connect, and Entity Framework were not copied.

The chatbot files stay in the tree and are unused. The layout does not render them. The page does not load their scripts or styles.

### MIT License

Copyright (c) .NET Foundation and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
