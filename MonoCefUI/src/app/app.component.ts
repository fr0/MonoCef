import {ChangeDetectorRef, Component, OnChanges, SimpleChanges} from '@angular/core';
import {DataMarshallerService} from './data-marshaler.service';

interface ShapeSettings {
  circles: number;
  squares: number;
  triangles: number;
}

const DefaultSettings: ShapeSettings = {
  circles: 3,
  squares: 3,
  triangles: 3
};

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnChanges {
  settings: ShapeSettings;
  constructor(private dataService: DataMarshallerService, private cdr: ChangeDetectorRef) {
    this.settings = dataService.data || DefaultSettings;
    dataService.changed.subscribe(data => {
      this.settings = data;
    });
  }
  ngOnChanges(changes: SimpleChanges) {
    this.dataService.update(this.settings);
  }
  update() {
    this.dataService.update(this.settings);
  }
}
