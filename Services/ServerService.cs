using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Authors;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Features.BooksProgress;
using BookHeaven.Domain.Features.Fonts;
using BookHeaven.Domain.Features.ProfileSettingss;
using BookHeaven.Domain.Features.Seriess;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using MediatR;
using Font = BookHeaven.Domain.Entities.Font;

namespace BookHeaven.Reader.Services
{
	public interface IServerService
	{
		Task<bool> CanConnect();
		Task<List<Book>?> GetAllBooks();
		Task<List<Author>?> GetAllAuthors();
		Task<List<Profile>> GetAllProfiles();
		Task<BookProgress?> GetBookProgress(Guid profileId, Guid bookId);
		Task DownloadBook(Book book, Guid profileId);
		Task UpdateBookProgress(BookProgress progress);
		Task UpdateProgressByProfile(Guid profileId);
		Task DownloadFonts();
	}
	public class ServerService(
		ISender sender,
		AppStateService appStateService, 
		ILogger<ServerService> logger) : IServerService
	{
		private HttpClient _httpClient = new();

		public async Task<bool> CanConnect()
		{
			if(Connectivity.Current.NetworkAccess == NetworkAccess.None)
			{
				return false;
			}
			
			var url = appStateService.ServerUrl;

			if (string.IsNullOrEmpty(url))
			{
				return false;
			}
			
			if(_httpClient.BaseAddress == null)
			{
				_httpClient = new HttpClient
				{
					BaseAddress = new Uri(url)
				};
			}

			try
			{
				var response = await _httpClient.GetAsync("api/ping");
				return response.IsSuccessStatusCode;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to connect to server");
				return false;
			}
		}

		public async Task<List<Book>?> GetAllBooks()
		{
			var endpoint = "api/books";

			try
			{
				var response = await _httpClient.GetFromJsonAsync<List<Book>?>(endpoint);

				return response;
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to get books from server", ex);
			}
		}

		public async Task<List<Author>?> GetAllAuthors()
		{
			var endpoint = "api/authors";

			try
			{
				var response = await _httpClient.GetFromJsonAsync<List<Author>>(endpoint);

				return response;
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to get authors from server", ex);
			}
		}

		public async Task<List<Profile>> GetAllProfiles()
		{
			var endpoint = "api/profiles";

			try
			{
				var response = await _httpClient.GetFromJsonAsync<List<Profile>>(endpoint);

				return response!;
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to get profiles from server", ex);
			}
		}

		public async Task<BookProgress?> GetBookProgress(Guid profileId, Guid bookId)
		{
			var endpoint = $"api/profiles/{profileId}/{bookId}";
			try
			{
				var response = await _httpClient.GetFromJsonAsync<BookProgress>(endpoint);

				return response;
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to get book progress from server", ex);
			}
		}

		public async Task DownloadBook(Book book, Guid profileId)
		{
			try
			{
				var getBook = await sender.Send(new GetBookQuery(book.BookId));
				if (getBook.IsSuccess)
				{
					//If the book is already downloaded, we remove the local cache
					Directory.EnumerateFiles(MauiProgram.BooksPath).Where(f => f.StartsWith(book.BookId.ToString())).ToList().ForEach(File.Delete);
					await sender.Send(new UpdateBookCommand(book));
				}
				else
				{
					await sender.Send(new AddBook.Command(book));
				}

				await DownloadFile(book.EpubUrl(), book.BookId);
				await DownloadFile(book.CoverUrl(), book.BookId);
				
				var progress = await GetBookProgress(profileId, book.BookId);
				var getCurrentProgress = await sender.Send(new GetBookProgressByProfile.Query(book.BookId, profileId));

				if (getCurrentProgress.IsFailure && progress != null)
				{
					await sender.Send(new AddBookProgress.Command(progress));
				}
				else if (getCurrentProgress.IsSuccess && progress != null &&
				         progress.LastRead >= getCurrentProgress.Value.LastRead)
				{
					progress.BookWordCount = 0;
					await sender.Send(new UpdateBookProgress.Command(progress));
				}
			}
			catch (Exception ex)
			{
				await Toast.Make(ex.Message, ToastDuration.Long).Show();
			}
		}

		public async Task UpdateBookProgress(BookProgress progress)
		{
			var endpoint = "api/progress/update";

			try
			{
				var response = await _httpClient.PostAsJsonAsync(endpoint, progress);
				if(!response.IsSuccessStatusCode)
				{
					var errorResponse = await response.Content.ReadAsStringAsync();
					throw new Exception($"Server responded with {response.StatusCode}: {errorResponse}");
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to update book progress on server", ex);
			}
		}


		private async Task DownloadFile(string url, Guid id)
		{
			var extension = url.Split('.').Last();
			var path = extension == "epub" ? MauiProgram.BooksPath : MauiProgram.CoversPath;

			var fileBytes = await _httpClient.GetByteArrayAsync(url[1..]);
			await File.WriteAllBytesAsync(Path.Combine(path, $"{id}.{extension}"), fileBytes);
		}

		public async Task UpdateProgressByProfile(Guid profileId)
		{
			var getProgresses = await sender.Send(new GetAllBooksProgressByProfile.Query(profileId));
			if (getProgresses.IsFailure)
			{
				return;
			}
			
			foreach (var progress in getProgresses.Value)
			{
				await UpdateBookProgress(progress);
			}
		}

		public async Task DownloadFonts()
		{
			var endpoint = "api/fonts";
			try
			{
				var response = await _httpClient.GetFromJsonAsync<List<Font>>(endpoint);

				if (response == null)
				{
					throw new Exception("Failed to get fonts from server");
				}

				foreach (var font in response)
				{
					var fontPath = font.FilePath(MauiProgram.FontsPath);
					var folderPath = font.FolderPath(MauiProgram.FontsPath);
					if (!Directory.Exists(folderPath))
					{
						Directory.CreateDirectory(folderPath);
					}
					
					var fileBytes = await _httpClient.GetByteArrayAsync(appStateService.ServerUrl + font.Url());
					await File.WriteAllBytesAsync(fontPath, fileBytes);

					await sender.Send(new AddFont.Command(font));
				}
				
				var getProfileSettings = await sender.Send(new GetProfileSettings.Query(appStateService.ProfileId));
				if (getProfileSettings.IsSuccess)
				{
					getProfileSettings.Value.SelectedFont = response.FirstOrDefault()?.Family ?? string.Empty;
					await sender.Send(new UpdateProfileSettings.Command(getProfileSettings.Value));
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to download fonts from server", ex);
			}
		}
	}
}
