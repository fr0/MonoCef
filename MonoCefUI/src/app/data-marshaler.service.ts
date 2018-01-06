import {EventEmitter, Injectable} from '@angular/core';

@Injectable()
export class DataMarshallerService {
  private changeCount = 0;
  public data: any;
  public changed: EventEmitter<any> = new EventEmitter<any>();
  constructor() {
    this.data = window['initialData'];
    window['pushData'] = data => {
      this.data = JSON.parse(data);
      this.changed.emit(this.data);
    };
    window['pullData'] = () => {
      return JSON.stringify(this.data);
    };
    window['changeCount'] = () => {
      return this.changeCount;
    };
  }
  update(newData) {
    this.data = newData;
    this.changeCount++;
  }
}

