import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

/** The texts of Material's paginator, in Hebrew. */
@Injectable()
export class HebrewPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = 'שורות בעמוד:';
  override nextPageLabel = 'העמוד הבא';
  override previousPageLabel = 'העמוד הקודם';
  override firstPageLabel = 'העמוד הראשון';
  override lastPageLabel = 'העמוד האחרון';

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0) {
      return '0 מתוך 0';
    }
    const first = page * pageSize + 1;
    const last = Math.min((page + 1) * pageSize, length);
    return `${first.toLocaleString('he-IL')}–${last.toLocaleString('he-IL')} מתוך ${length.toLocaleString('he-IL')}`;
  };
}
