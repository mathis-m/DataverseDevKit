import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { ComponentResult } from '../types';
import { makeStyles, tokens, Button } from '@fluentui/react-components';
import { ZoomIn20Regular, ZoomOut20Regular, ArrowReset20Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  container: {
    position: 'relative',
    width: '100%',
    height: '100%',
  },
  controls: {
    position: 'absolute',
    top: tokens.spacingVerticalM,
    right: tokens.spacingHorizontalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    zIndex: 10,
  },
  svg: {
    width: '100%',
    height: '100%',
    cursor: 'grab',
    ':active': {
      cursor: 'grabbing',
    },
  },
});

interface LayerTreemapProps {
  components: ComponentResult[];
  width?: number;
  height?: number;
}

export const LayerTreemap: React.FC<LayerTreemapProps> = ({
  components,
  width = 1200,
  height = 800,
}) => {
  const styles = useStyles();
  const svgRef = useRef<SVGSVGElement>(null);
  const [zoom, setZoom] = useState<d3.ZoomBehavior<SVGSVGElement, unknown> | null>(null);

  useEffect(() => {
    if (!svgRef.current || components.length === 0) return;

    // Clear previous content
    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    // Prepare hierarchical data: ComponentType -> Components
    const typeMap = new Map<string, ComponentResult[]>();

    components.forEach(component => {
      if (!typeMap.has(component.componentType)) {
        typeMap.set(component.componentType, []);
      }
      typeMap.get(component.componentType)!.push(component);
    });

    // Create hierarchy
    const hierarchyData: any = {
      name: 'Components',
      children: Array.from(typeMap.entries()).map(([type, comps]) => ({
        name: type,
        children: comps.map(c => ({
          name: c.displayName || c.logicalName,
          value: c.layerSequence.length, // Size by layer count
          component: c,
        })),
      })),
    };

    const root = d3.hierarchy(hierarchyData)
      .sum((d: any) => d.value || 0)
      .sort((a, b) => (b.value || 0) - (a.value || 0));

    const treemap = d3.treemap()
      .size([width, height])
      .paddingOuter(3)
      .paddingTop(20)
      .paddingInner(1)
      .round(true);

    treemap(root as any);

    // Create zoom behavior
    const zoomBehavior = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 10])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
      });

    svg.call(zoomBehavior);
    setZoom(zoomBehavior as any);

    const g = svg.append('g');

    const color = d3.scaleOrdinal(d3.schemeTableau10);

    // Draw rectangles
    const cell = g.selectAll('g')
      .data(root.leaves())
      .join('g')
      .attr('transform', (d: any) => `translate(${d.x0},${d.y0})`)
      .style('cursor', 'pointer');

    cell.append('rect')
      .attr('width', (d: any) => d.x1 - d.x0)
      .attr('height', (d: any) => d.y1 - d.y0)
      .attr('fill', (d: any) => color(d.parent.data.name))
      .attr('stroke', '#fff')
      .attr('stroke-width', 1)
      .attr('opacity', 0.8)
      .on('mouseover', function() {
        d3.select(this).attr('opacity', 1);
      })
      .on('mouseout', function() {
        d3.select(this).attr('opacity', 0.8);
      });

    // Add text
    cell.append('text')
      .attr('x', 4)
      .attr('y', 15)
      .style('font-size', '11px')
      .style('fill', '#000')
      .style('pointer-events', 'none')
      .text((d: any) => {
        const width = d.x1 - d.x0;
        const name = d.data.name;
        if (width < 40) return '';
        if (name.length > width / 6) {
          return name.substring(0, Math.floor(width / 6)) + '...';
        }
        return name;
      });

    // Add layer count
    cell.append('text')
      .attr('x', 4)
      .attr('y', 30)
      .style('font-size', '10px')
      .style('fill', '#666')
      .style('pointer-events', 'none')
      .text((d: any) => {
        const width = d.x1 - d.x0;
        if (width < 40) return '';
        return `${d.value} layers`;
      });

    cell.append('title')
      .text((d: any) => {
        const comp = d.data.component;
        return `${comp.displayName || comp.logicalName}\nType: ${comp.componentType}\nLayers: ${comp.layerSequence.join(' → ')}\nTotal: ${d.value} layers`;
      });

    // Draw group labels (component types)
    const groupLabels = g.selectAll('.group-label')
      .data(root.children || [])
      .join('g')
      .attr('class', 'group-label')
      .attr('transform', (d: any) => `translate(${d.x0},${d.y0})`);

    groupLabels.append('rect')
      .attr('width', (d: any) => d.x1 - d.x0)
      .attr('height', 18)
      .attr('fill', (d: any) => color(d.data.name))
      .attr('opacity', 0.3);

    groupLabels.append('text')
      .attr('x', 4)
      .attr('y', 13)
      .style('font-size', '12px')
      .style('font-weight', 'bold')
      .style('fill', '#000')
      .style('pointer-events', 'none')
      .text((d: any) => {
        const width = d.x1 - d.x0;
        const name = d.data.name;
        const count = d.children.length;
        const text = `${name} (${count})`;
        if (text.length > width / 7) {
          return text.substring(0, Math.floor(width / 7)) + '...';
        }
        return text;
      });

  }, [components, width, height]);

  const handleZoomIn = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.scaleBy as any, 1.3);
    }
  };

  const handleZoomOut = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.scaleBy as any, 0.7);
    }
  };

  const handleReset = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.transform as any, d3.zoomIdentity);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.controls}>
        <Button icon={<ZoomIn20Regular />} onClick={handleZoomIn} title="Zoom In" />
        <Button icon={<ZoomOut20Regular />} onClick={handleZoomOut} title="Zoom Out" />
        <Button icon={<ArrowReset20Regular />} onClick={handleReset} title="Reset View" />
      </div>
      <svg ref={svgRef} className={styles.svg} width={width} height={height} />
    </div>
  );
};
