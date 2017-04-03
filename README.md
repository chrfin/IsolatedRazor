# IsolatedRazor
Offers a secure and isolated template rendering engine using the Razor syntax. This project is heavily based on [RazorEngine](https://github.com/Antaris/RazorEngine).

The Razor templates are compiled into temporary assemblies (cached) and then executed in a secure AppDomain which does not has access to the disk, database or the network.

Supports ASP.NET (dynamic ViewBag in templates) and shadow-copying (shadow-copied assembly can contain model class).
To install IsolatedRazor, run the following command in the Package Manager Console:

> PM> Install-Package IsolatedRazor

# Usage
To see IsolatedRazor in action you can check the `IsolatedRazor.Demo.*` projects.

## Short usage example:

    using (var templater = new IsolatedRazor.RazorTemplater(templatePath))
    {
        string result = await templater.ParseAsync("MyTemplate",
            "<div>Hello @Model.Name! It is @DateTime.Now</div>", timestamp, model).ConfigureAwait(false);
    }

Constructing the `IsolatedRazor.RazorTemplater` instance is rather slow, so if you can you should reuse it.
