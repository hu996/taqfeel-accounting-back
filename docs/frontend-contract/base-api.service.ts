import { environment } from '../../../environments/environment';

export abstract class BaseApiService {
  protected url(path: string): string {
    const base = environment.baseUrl.endsWith('/')
      ? environment.baseUrl
      : `${environment.baseUrl}/`;
    const cleanPath = path.replace(/^\/+/, '').replace(/^api\//i, '');

    return `${base}${cleanPath}`;
  }

  protected withId(path: string, id: string): string {
    return `${this.url(path)}/${encodeURIComponent(id)}`;
  }
}
