using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Services;

namespace BookHeaven.Reader.Services
{
	public interface IServerService
	{
		Task<bool> CanConnect(string? url);
		Task<List<Book>?> GetAllBooks();
		Task<List<Author>?> GetAllAuthors();
		Task<List<Profile>> GetAllProfiles();
		Task<BookProgress?> GetBookProgress(Guid profileId, Guid bookId);
		Task Download(Book book, Guid profileId);
		Task UpdateBookProgress(BookProgress progress);
		Task UpdateProgressByProfile(Guid profileId);
	}
	public class ServerService(IDatabaseService databaseService, ILogger<ServerService> logger) : IServerService
	{
		private HttpClient _httpClient = new();

		public async Task<bool> CanConnect(string? url)
		{
			if(Connectivity.Current.NetworkAccess == NetworkAccess.None)
			{
				return false;
			}

			if (string.IsNullOrEmpty(url) || !url.StartsWith("https://"))
			{
				return false;
			}
			
			if (_httpClient.BaseAddress == null)
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
			string endpoint = "api/books";

			try
			{
				List<Book>? response = await _httpClient.GetFromJsonAsync<List<Book>?>(endpoint);

				return response;
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to get books from server", ex);
			}
		}

		public async Task<List<Author>?> GetAllAuthors()
		{
			string endpoint = "api/authors";

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
			string endpoint = "api/profiles";

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
			string endpoint = $"api/profiles/{profileId}/{bookId}";
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

		public async Task Download(Book book, Guid profileId)
		{
			try
			{

				if(await databaseService.Get<Book>(book.BookId) != null)
				{
					//If the book is already downloaded, we remove the local cache
					Directory.EnumerateFiles(MauiProgram.BooksPath).Where(f => f.StartsWith(book.BookId.ToString())).ToList().ForEach(File.Delete);
				}

				/*
				 * We make a clone of the book because if we keep the relations, entity framework will try to insert the author and series again and complain if they already
				 * exist before downloading it.
				 * This way we can remove the relations without affecting the UI and use the generic Save method instead of having a specific one just for the book.
				 */
				Book localBook = book.Clone();
				if (localBook.Author != null)
				{
					await databaseService.AddOrUpdate(localBook.Author, true);
				}
				if (localBook.Series != null)
				{
					await databaseService.AddOrUpdate(localBook.Series, true);
				}

				localBook.Author = null;
				localBook.Series = null;

				await databaseService.AddOrUpdate(localBook);
				await databaseService.SaveChanges();

				await DownloadFile(localBook.BookUrl, localBook.BookId);
				await DownloadFile(localBook.CoverUrl, localBook.BookId);

				var localProgress = await databaseService.GetBy<BookProgress>(p => p.ProfileId == profileId && p.BookId == localBook.BookId);
				var progress = await GetBookProgress(profileId, localBook.BookId);

				if((localProgress == null && progress != null) || (localProgress != null && progress != null && progress.LastRead >= localProgress.LastRead))
				{
					progress.BookWordCount = 0;
					await databaseService.AddOrUpdate(progress);
					await databaseService.SaveChanges();
				}

				
			}
			catch (Exception ex)
			{
				throw new Exception("Failed to download book from server", ex);
			}
		}

		public async Task UpdateBookProgress(BookProgress progress)
		{
			string endpoint = "api/progress/update";

			try
			{
				HttpResponseMessage response = await _httpClient.PostAsJsonAsync(endpoint, progress);
				if(!response.IsSuccessStatusCode)
				{
					string errorResponse = await response.Content.ReadAsStringAsync();
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
			var profile = await databaseService.GetIncluding<Profile>(profileId, p => p.BooksProgress);

			if(profile == null || profile.BooksProgress.Count == 0)
			{
				return;
			}

			foreach (var cleanProgress in profile.BooksProgress.Select(progress => progress.Clone()))
			{
				cleanProgress.Profile = null;
				cleanProgress.Book = null;
				await UpdateBookProgress(cleanProgress);
			}
		}
	}
}
