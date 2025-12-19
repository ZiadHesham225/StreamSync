import { PaginationQuery } from '../types/index';

export const paginationUtils = {
  buildQueryParams(pagination?: PaginationQuery): URLSearchParams {
    const params = new URLSearchParams();
    if (pagination) {
      if (pagination.page !== undefined) params.append('page', pagination.page.toString());
      if (pagination.pageSize !== undefined) params.append('pageSize', pagination.pageSize.toString());
      if (pagination.search) params.append('search', pagination.search);
      if (pagination.sortBy) params.append('sortBy', pagination.sortBy);
      if (pagination.sortDescending !== undefined) {
        params.append('SortOrder', pagination.sortDescending ? 'desc' : 'asc');
      }
    }
    return params;
  },

  buildQueryString(pagination?: PaginationQuery): string {
    const params = this.buildQueryParams(pagination);
    const queryString = params.toString();
    return queryString ? `?${queryString}` : '';
  }
};
