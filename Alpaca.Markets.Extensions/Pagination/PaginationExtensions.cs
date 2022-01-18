﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Alpaca.Markets.Extensions
{
    internal static class PaginationExtensions
    {
        public static async IAsyncEnumerable<TItem> GetResponsesByItems<TRequest, TItem>(
            this TRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<TRequest, CancellationToken, Task<IPage<TItem>>> getSinglePage,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TRequest : HistoricalRequestBase
        {
            await foreach (var page in GetResponsesByPages(
                    singlePageOfItemsRequestWithEmptyPageToken, getSinglePage, cancellationToken)
                .ConfigureAwait(false))
            {
                foreach (var item in page)
                {
                    yield return item;
                }
            }
        }

        public static async IAsyncEnumerable<INewsArticle> GetResponsesByItems(
            this NewsArticlesRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<NewsArticlesRequest, CancellationToken, Task<IPage<INewsArticle>>> getSinglePage,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var page in GetResponsesByPages(
                    singlePageOfItemsRequestWithEmptyPageToken, getSinglePage, cancellationToken)
                .ConfigureAwait(false))
            {
                foreach (var item in page)
                {
                    yield return item;
                }
            }
        }

        public static IReadOnlyDictionary<String, IAsyncEnumerable<TItem>> GetResponsesByItems<TRequest, TItem>(
            this TRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<TRequest, CancellationToken, Task<IMultiPage<TItem>>> getSinglePage,
            CancellationToken cancellationToken = default)
            where TRequest : HistoricalRequestBase
        {
            var channelsBySymbols =
                singlePageOfItemsRequestWithEmptyPageToken.Symbols
                    .ToDictionary(_ => _, _ => Channel.CreateUnbounded<TItem>(),
                        StringComparer.Ordinal);

            Task.Run(GetResponsesByItemsImpl, cancellationToken);

            return channelsBySymbols.ToDictionary(
                _ => _.Key, _ => ReadAllAsync(_.Value.Reader, cancellationToken),
                StringComparer.Ordinal);

            async Task GetResponsesByItemsImpl()
            {
                await foreach (var page in GetResponsesByPages(
                        singlePageOfItemsRequestWithEmptyPageToken, getSinglePage, cancellationToken)
                    .ConfigureAwait(false))
                {
                    foreach (var kvp in page)
                    {
                        await WriteAllAsync(channelsBySymbols[kvp.Key].Writer, kvp.Value, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                foreach (var channel in channelsBySymbols.Values)
                {
                    channel.Writer.TryComplete();
                }
            }

            static async IAsyncEnumerable<T> ReadAllAsync<T>(
                ChannelReader<T> reader,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        yield return item;
                    }
                }
            }

            static async ValueTask WriteAllAsync<T>(
                ChannelWriter<T> writer,
                IEnumerable<T> items,
                CancellationToken cancellationToken)
            {
                foreach (var item in items)
                {
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public static IAsyncEnumerable<IReadOnlyList<TItem>> GetResponsesByPages<TRequest, TItem>(
            this TRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<TRequest, CancellationToken, Task<IPage<TItem>>> getSinglePage,
            CancellationToken cancellationToken = default)
            where TRequest : HistoricalRequestBase =>
            getResponses(
                singlePageOfItemsRequestWithEmptyPageToken,
                (request, ct) => getItemsAndNextPageToken(getSinglePage, request, ct),
                cancellationToken);

        public static IAsyncEnumerable<IReadOnlyList<INewsArticle>> GetResponsesByPages(
            this NewsArticlesRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<NewsArticlesRequest, CancellationToken, Task<IPage<INewsArticle>>> getSinglePage,
            CancellationToken cancellationToken = default) =>
            getResponses(
                singlePageOfItemsRequestWithEmptyPageToken,
                (request, ct) => getItemsAndNextPageToken(getSinglePage, request, ct),
                cancellationToken);

        public static IAsyncEnumerable<IReadOnlyDictionary<String, IReadOnlyList<TItem>>> GetResponsesByPages<TRequest, TItem>(
            this TRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<TRequest, CancellationToken, Task<IMultiPage<TItem>>> getSinglePage,
            CancellationToken cancellationToken = default)
            where TRequest : HistoricalRequestBase =>
            getResponses(
                singlePageOfItemsRequestWithEmptyPageToken,
                (request, ct) => getItemsAndNextPageToken(getSinglePage, request, ct),
                cancellationToken);

        private static async IAsyncEnumerable<TResponse> getResponses<TRequest, TResponse>(
            TRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<TRequest, CancellationToken, Task<(TResponse, String?)>> getItemsAndNextPageToken,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TRequest : HistoricalRequestBase
        {
            var request = singlePageOfItemsRequestWithEmptyPageToken;
            do
            {
                var (items, nextPageToken) = await getItemsAndNextPageToken(
                    request, cancellationToken).ConfigureAwait(false);

                yield return items;

                request = request.WithPageToken(nextPageToken ?? String.Empty);
            } while (!String.IsNullOrEmpty(request.Pagination.Token));
        }

        private static async IAsyncEnumerable<IReadOnlyList<INewsArticle>> getResponses(
            NewsArticlesRequest singlePageOfItemsRequestWithEmptyPageToken,
            Func<NewsArticlesRequest, CancellationToken, Task<(IReadOnlyList<INewsArticle>, String?)>> getItemsAndNextPageToken,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = singlePageOfItemsRequestWithEmptyPageToken;
            do
            {
                var (items, nextPageToken) = await getItemsAndNextPageToken(
                    request, cancellationToken).ConfigureAwait(false);

                yield return items;

                request = request.WithPageToken(nextPageToken ?? String.Empty);
            } while (!String.IsNullOrEmpty(request.Pagination.Token));
        }

        private static async Task<(IReadOnlyList<TItem>, String?)> getItemsAndNextPageToken<TRequest, TItem>(
            this Func<TRequest, CancellationToken, Task<IPage<TItem>>> getSinglePage,
            TRequest request,
            CancellationToken cancellationToken)
        {
            var response = await getSinglePage(request, cancellationToken).ConfigureAwait(false);
            return (response.Items, response.NextPageToken);
        }

        private static async Task<(IReadOnlyDictionary<String, IReadOnlyList<TItem>>, String?)> getItemsAndNextPageToken<TRequest, TItem>(
            this Func<TRequest, CancellationToken, Task<IMultiPage<TItem>>> getSinglePage,
            TRequest request,
            CancellationToken cancellationToken)
        {
            var response = await getSinglePage(request, cancellationToken).ConfigureAwait(false);
            return (response.Items, response.NextPageToken);
        }

    }
}
