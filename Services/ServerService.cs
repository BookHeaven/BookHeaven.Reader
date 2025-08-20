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
using BookHeaven.Domain.Shared;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using MediatR;
using Font = BookHeaven.Domain.Entities.Font;

namespace BookHeaven.Reader.Services
{
	public interface IServerService
	{
		Task<Result> CanConnect();
		Task<Result<List<Book>>> GetAllBooks();
		Task<Result<List<Author>>> GetAllAuthors();
		Task<Result<List<Profile>>> GetAllProfiles();
		Task<Result<BookProgress?>> GetBookProgress(Guid profileId, Guid bookId);
		Task<Result> DownloadBook(Book book, Guid profileId);
		Task<Result> UpdateBookProgress(BookProgress progress);
		Task<Result> UpdateProgressByProfile(Guid profileId);
		Task<Result> UpdateProfileSettings(ProfileSettings settings);
		Task<Result> DownloadFonts();
	}
	public class ServerService(
		ISender sender,
		AppStateService appStateService, 
		ILogger<ServerService> logger) : IServerService
	{
		private HttpClient _httpClient = new();

		public async Task<Result> CanConnect()
		{
			if(Connectivity.Current.NetworkAccess == NetworkAccess.None)
			{
				return Result.Failure(new Error("No internet connection"));
			}
			
			var url = appStateService.ServerUrl;

			if (string.IsNullOrEmpty(url))
			{
				return new Error("Server URL is not set");
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
				return response.IsSuccessStatusCode 
					? Result.Success()
					: Result.Failure(new Error("Failed to connect to server"));;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to connect to server");
				return new Error("Failed to connect to server");
			}
		}

		public async Task<Result<List<Book>>> GetAllBooks()
		{
			var endpoint = "api/books";

			try
			{
				var response = await _httpClient.GetFromJsonAsync<List<Book>?>(endpoint);

				return response ?? [];
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to get books from server");
				return new Error("Failed to get books from server");
			}
		}

		public async Task<Result<List<Author>>> GetAllAuthors()
		{
			var endpoint = "api/authors";

			try
			{
				var response = await _httpClient.GetFromJsonAsync<List<Author>>(endpoint);

				return response ?? [];
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to get authors from server");
				return new Error("Failed to get authors from server");
			}
		}

		public async Task<Result<List<Profile>>> GetAllProfiles()
		{
			var endpoint = "api/profiles";

			try
			{
				var response = await _httpClient.GetFromJsonAsync<List<Profile>>(endpoint);

				return response ?? [];
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to get profiles from server");
				return new Error("Failed to get profiles from server");
			}
		}

		public async Task<Result<BookProgress?>> GetBookProgress(Guid profileId, Guid bookId)
		{
			var endpoint = $"api/profiles/{profileId}/{bookId}";
			try
			{
				var response = await _httpClient.GetFromJsonAsync<BookProgress>(endpoint);

				return response;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to get book progress from server");
				return new Error("Failed to get book progress from server");
			}
		}

		public async Task<Result> DownloadBook(Book book, Guid profileId)
		{
			try
			{
				var getBook = await sender.Send(new GetBook.Query(book.BookId));
				if (getBook.IsSuccess)
				{
					//If the book is already downloaded, we remove the local cache
					Directory.EnumerateFiles(MauiProgram.BooksPath).Where(f => f.StartsWith(book.BookId.ToString())).ToList().ForEach(File.Delete);
					await sender.Send(new UpdateBook.Command(book));
				}
				else
				{
					await sender.Send(new AddBook.Command(book));
				}

				await DownloadFile(book.EpubUrl(), book.BookId);
				await DownloadFile(book.CoverUrl(), book.BookId);
				
				var getProgress = await GetBookProgress(profileId, book.BookId);
				if (getProgress.IsFailure)
				{
					return new Error("Failed to get book progress from server");
				}
				
				var progress = getProgress.Value;
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
				
				return Result.Success();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to download book from server");
				return new Error("Failed to download book from server");
			}
		}

		public async Task<Result> UpdateBookProgress(BookProgress progress)
		{
			var endpoint = "api/progress/update";

			try
			{
				var response = await _httpClient.PutAsJsonAsync(endpoint, progress);
				if(!response.IsSuccessStatusCode)
				{
					var errorResponse = await response.Content.ReadAsStringAsync();
					return new Error("Failed to update book progress from server");
				}
				return Result.Success();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to update book progress from server");
				return new Error("Failed to update book progress from server");
			}
		}


		private async Task DownloadFile(string url, Guid id)
		{
			var extension = url.Split('.').Last();
			var path = extension == "epub" ? MauiProgram.BooksPath : MauiProgram.CoversPath;

			var fileBytes = await _httpClient.GetByteArrayAsync(url[1..]);
			await File.WriteAllBytesAsync(Path.Combine(path, $"{id}.{extension}"), fileBytes);
		}

		public async Task<Result> UpdateProgressByProfile(Guid profileId)
		{
			var getProgresses = await sender.Send(new GetAllBooksProgressByProfile.Query(profileId));
			if (getProgresses.IsFailure)
			{
				return getProgresses.Error;
			}
			
			foreach (var progress in getProgresses.Value)
			{
				var updateProgress = await UpdateBookProgress(progress);
				if (updateProgress.IsFailure)
				{
					return updateProgress.Error;
				}
			}
			return Result.Success();
		}
		
		public async Task<Result> UpdateProfileSettings(ProfileSettings settings)
		{
			var endpoint = "api/profile/settings/update";

			try
			{
				var response = await _httpClient.PutAsJsonAsync(endpoint, settings);
				if(!response.IsSuccessStatusCode)
				{
					var errorResponse = await response.Content.ReadAsStringAsync();
					return new Error("Failed to backup profile settings to server");
				}
				return Result.Success();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to backup profile settings to server");
				return new Error("Failed to backup profile settings to server");
			}
		}

		public async Task<Result> DownloadFonts()
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
					var fontPath = font.FilePath();
					var folderPath = font.FolderPath();
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
				return Result.Success();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to download fonts from server");
				return new Error("Failed to download fonts from server");
			}
		}
	}
}
