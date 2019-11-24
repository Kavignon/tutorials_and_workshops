module Client

open Elmish
open Elmish.React
open Feliz
open Feliz.Router
open Fable.Remoting.Client

open Shared
open BackendInteractions
open BackendInteractions.Cmd

module ApplicationModel =
    type AppPage =
        | LandingPage of DelayedResult<Result<StoreProduct list, string>>
        | ProductDetails of StoreProduct
        | NotFound

    type Model = {
         CurrentPage : AppPage
         CurrentUrl  : string list
     }

    type EventMessage =
        | FetchProducts of AsyncTransaction<Result<StoreProduct list, string>>
        | UrlChanged of string list
        | LoadProductPage of StoreProduct
        | ProductPageLoaded

    let init() =
        let initialModel = { CurrentUrl = [ ]; CurrentPage = LandingPage OperationNotStarted }
        let initialEventMsg = Cmd.ofMsg (FetchProducts Begin)
        initialModel, initialEventMsg

open ApplicationModel

module UrlHandling =
    open Browser

    let hashPrefix = sprintf "#%s"

    let combine xs = List.fold (sprintf "%s/%s") "" xs

    let navigationEvent = "NavigationEvent"

    let navigate xs : Elmish.Cmd<_> =
        let nextUrl = hashPrefix (combine xs)
        [ fun _ ->
            history.pushState ((), "", nextUrl)
            let ev = CustomEvent.Create ""
            ev.initEvent (navigationEvent, true, true)
            window.dispatchEvent ev |> ignore ]

    let updateSiteUrl (state: Model) =
        match state.CurrentPage with
        | LandingPage _ -> { state with CurrentUrl = [] }
        | ProductDetails product -> { state with CurrentUrl = [ "products"; product.Id ] }
        | NotFound -> { state with CurrentUrl = [ "Not Found"] }

let loadStoreProducts = async {
    do! Async.Sleep 1000
    let! products = remoteWebStoreApi.fetchProducts()
    if products.Length = 0 then
        return FetchProducts (Completed (Error "No products were loaded from the catalogue"))
    else
        return FetchProducts (Completed (Ok products))
}

let update (msg : EventMessage) (currentModel : Model) : Model * Cmd<EventMessage> =
    match msg with
    | FetchProducts Begin ->
      let nextState = { currentModel with CurrentPage = LandingPage OperationInProgress }
      let nextCmd = fromAsync loadStoreProducts
      nextState, nextCmd

    | FetchProducts (Completed products) ->
      let nextState = UrlHandling.updateSiteUrl { currentModel with CurrentPage = LandingPage (ResultFromServer products) }
      nextState, Cmd.none

    | UrlChanged urlSegments ->
        let nextState = { currentModel with CurrentUrl = urlSegments }
        nextState, Cmd.none

    | LoadProductPage product ->
        let nextState = UrlHandling.updateSiteUrl { currentModel with CurrentPage = ProductDetails product }
        nextState, UrlHandling.navigate [ product.Id ]

    | ProductPageLoaded ->
        currentModel, Cmd.none

module ApplicationView =
    let div (classes: string list) (children: ReactElement list) =
      Html.div [
        prop.className classes
        prop.children children
      ]

    let productDetailView (product: StoreProduct) =
        Html.div [
            prop.className (sprintf "product-page-idd%s" product.Id)
            prop.style [ style.marginTop 20; style.marginBottom 20 ]
            prop.children [
                Html.div [
                    div [ "columns" ] [
                        div [ "column" ] [
                            Html.img [
                                prop.src product.Image
                                prop.alt "product image"
                                prop.style [
                                    style.width 400
                                    style.height 450
                                    style.paddingTop 35
                                ]
                            ]
                        ]
                        div [ "column" ] [
                            Html.div [
                                prop.className "details-container"
                                prop.fontSize 100
                                prop.style [ style.paddingRight 100; style.marginRight 100 ]
                                prop.children [
                                    Html.div [
                                        prop.className "detail-box-container"
                                        prop.children [
                                            Html.div [
                                                prop.className "box"
                                                prop.style [
                                                        style.marginTop 15
                                                        style.marginBottom 15
                                                    ]
                                                prop.children [
                                                    Html.div [
                                                        prop.text product.Name
                                                    ]
                                                    Html.div [
                                                        prop.text (sprintf "Price: CDN$ %.2f" product.Price)
                                                        prop.style [ style.color "blue" ]
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]

    let storeItemSummaryView (product: StoreProduct) dispatch =
        Html.div [
        prop.className "box"
        prop.style [
          style.marginTop 15
          style.marginBottom 15
        ]
        prop.children [
            div [ "columns"; "is-mobile"] [
                div [ "column"; "is-narrow" ] [
                    Html.div [
                        prop.className [ "product-icon" ]
                        prop.src product.Image
                        prop.style [ style.marginLeft 20 ]
                        prop.children [
                            Html.img [
                                prop.src product.Image
                                prop.alt "product image"
                                prop.style [
                                    style.width 120
                                    style.height 150
                                    style.paddingTop 15
                                ]
                            ]
                        ]
                    ]
                ]

                div [ "column" ] [
                    Html.div [
                        prop.style [
                            style.flexDirection.column
                            style.marginLeft 25
                            style.marginTop 10
                        ]
                        prop.fontSize 50
                        prop.children [
                            Html.a [
                                prop.fontSize 75
                                prop.width 50
                                prop.style [ style.textDecoration.underline ]
                                prop.text product.Name
                                prop.onClick (fun _ -> dispatch (LoadProductPage product) )
                            ]
                            Html.div [
                                prop.fontSize 25
                                prop.width 50
                                prop.text product.ReviewAverage
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

module ApplicationRendering =
    open ApplicationView

    let renderError (errorMsg: string) =
      Html.h1 [
        prop.style [ style.color.red ]
        prop.text errorMsg
      ]

    let renderSummaryViews products dispatch =
        products
        |> List.map(fun p -> storeItemSummaryView p dispatch)
        |> Html.fragment

    let spinner =
      Html.div [
        prop.style [ style.textAlign.center; style.marginTop 20 ]
        prop.children [
          Html.i [
            prop.className "fa fa-cog fa-spin fa-2x"
          ]
        ]
      ]

    let renderWebPage model dispatch =
        match model.CurrentPage with
        | LandingPage delayedPageModel ->
            match delayedPageModel with
            | OperationNotStarted -> Html.none
            | OperationInProgress -> spinner
            | ResultFromServer (Error errorMsg) -> renderError errorMsg
            | ResultFromServer (Ok items) -> renderSummaryViews items dispatch
        | ProductDetails storeProduct -> productDetailView storeProduct
        | NotFound -> Html.h1 "Not found"

    let render (state: Model) (dispatch: EventMessage -> unit) =
        let page =
            Html.div [
                prop.style [ style.padding 20 ]
                prop.children [
                  Html.h1 [ prop.className "title"; prop.text "SAFE E-Commerce Demo" ]
                  renderWebPage state dispatch
                ]
            ]

        Router.router [
            Router.onUrlChanged (UrlChanged >> dispatch)
            Router.application page
        ]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

open ApplicationRendering

Program.mkProgram init update render
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run