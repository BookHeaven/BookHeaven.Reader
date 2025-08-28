<p align="center">
  <img src="wwwroot/logo.svg" width="120px" alt="" />
</p>

<h1 align="center">BookHeaven Reader</h1>

BookHeaven Reader is part of the BookHeaven "ecosystem", which aims to provide a very convenient way to manage and read your ebook library.</br>
It's an Android app optimized for e-ink displays that connects to your BookHeaven Server instance to download and read your ebooks.

---

## :warning: Disclaimer
- You might need to update the WebView implementation of your device for the UI to render properly (pretty easy, more on the troubleshooting section below)
- It won't work as standalone, it needs to connect to a Server instance to get the books (not planned, but might happen eventually)
- Single page layout only (again, not planned, but might add better support for landscape eventually)
- No dictionary, no notes nor highlights
- Supports epub files only

> [!NOTE]
> If you haven't setup the Server yet, [check out this quick guide](https://bookheaven.ggarrido.dev/getting-started) to get started!

## :sparkles: So, what are the main features?
- :rocket: Can replace your launcher since it includes a basic but functional app drawer.
- :framed_picture: Can also be set as screensaver (Daydream) to show the cover of the book you are reading when the device enters standby mode.
- :cloud: Easily connect to your Server to download books, sync progress and backup your settings (no internet connection required for regular use).
- :clock12: It will track your reading progress (date started, last read, % read, elapsed time, etc)
- :book: Very customizable (font size, line height, page margins, paragraph spacing, indent, etc)
- :hand: Provides a few tactile navigation layouts to choose from and physical buttons should work as well (Only tested with Meebook M7)
- :boom: The images can be zoomed in and panned! :boom:

## :exclamation: Requirements
- Android 10+

## :globe_with_meridians: Supported UI Languages
- English
- Spanish

## :hammer_and_wrench: Troubleshooting
### Updating the Webview
The app is web based and it uses technologies that might bee too modern for the included WebView implementation of your Android device.
Luckily, it's very likely that you'll be able to replace it with an updated one from the Play Store.
Steps might vary for your device, but overall this is what you need to do:
1. Go to the Play Store, search for "Android System Webview" and install it
  > [!NOTE]
  > Many versions will be listed. Ideally you want the one that's just called "Android System Webview", but for me only "Dev", "Canary" and "Beta" show up.</br>
  > I use the Beta version personally and it works just fine.  
  > If you want the actual stable release, which might not show up, you can go to the Play Store from your pc and install it to your device from there.<br/>
  > Here's the link: [https://play.google.com/store/apps/details?id=com.google.android.webview](https://play.google.com/store/apps/details?id=com.google.android.webview)<br/>

2. Enable the developer settings if you haven't already
3. Look for WebView Implementation, and change it to the one you just downloaded
  > [!NOTE]
  > If it doesn't show up, or doesn't allow you to change it, try restarting, or try going to Apps > Show System apps and disable the included WebView, then restart and check again.
4. Restart your device to apply the change
5. Profit

## :framed_picture: Screenshots
<table style="filter: grayscale(100%);">
  <tr>
    <td>
      <img src="https://bookheaven-web.pages.dev/img/reader-img.png" alt="" />
    </td>
    <td>
        <img src="https://bookheaven-web.pages.dev/img/reader-remote.png" alt="" />
    </td>
    <td>
      <img src="https://bookheaven-web.pages.dev/img/reader-book.png" alt="" />
    </td>
  </tr>
  <tr>
    <td>
      <img src="https://bookheaven-web.pages.dev/img/reader-index.png" alt="" />
    </td>
    <td>
        <img src="https://bookheaven-web.pages.dev/img/reader-text-settings.png" alt="" />
    </td>
    <td>
        <img src="https://bookheaven-web.pages.dev/img/reader-page-settings.png" alt="" />
    </td>
  </tr>
  <tr>
    <td>
      <img src="https://bookheaven-web.pages.dev/img/reader-apps.png" alt="" />
    </td>
    <td>
    </td>
    <td></td>
  </tr>
</table>

## :package: Credits
- Blazor.ContextMenu (https://github.com/stavroskasidis/BlazorContextMenu)
- BlazorPanzoom (https://github.com/shaigem/BlazorPanzoom)
