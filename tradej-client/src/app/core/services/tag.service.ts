import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Tag, TagCategory } from '../models/tag.model';

@Injectable({ providedIn: 'root' })
export class TagService {
  private http = inject(HttpClient);
  private readonly api = '/api/tagcategories';

  getCategories() {
    return this.http.get<TagCategory[]>(this.api);
  }

  createCategory(name: string, color: string) {
    return this.http.post<TagCategory>(this.api, { name, color });
  }

  updateCategory(id: number, name: string, color: string, sortOrder: number) {
    return this.http.put<void>(`${this.api}/${id}`, { name, color, sortOrder });
  }

  deleteCategory(id: number) {
    return this.http.delete<void>(`${this.api}/${id}`);
  }

  createTag(categoryId: number, name: string) {
    return this.http.post<Tag>(`${this.api}/${categoryId}/tags`, { categoryId, name });
  }

  renameTag(categoryId: number, tagId: number, name: string) {
    return this.http.put<void>(`${this.api}/${categoryId}/tags/${tagId}`, { name });
  }

  deleteTag(categoryId: number, tagId: number) {
    return this.http.delete<void>(`${this.api}/${categoryId}/tags/${tagId}`);
  }
}
